﻿using System;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Common;

namespace Trading.Strategies
{
    public class PositionCloserStrategy : PrimaryStrategy
    {
        private readonly decimal _priceToClose;
        private readonly PriceDirection _securityDesirableDirection;
        private readonly Sides _strategyOrderSide;

        public Security SecurityWithSignalToClose { get; set; }

        public PositionCloserStrategy(decimal priceToClose, PriceDirection securityDesirableDirection, decimal positionToClose)
        {
            _priceToClose = priceToClose;
            _securityDesirableDirection = securityDesirableDirection;
            _strategyOrderSide = positionToClose > 0 ? Sides.Sell : Sides.Buy;
            Volume = Math.Abs(positionToClose);
        }

        protected override void OnStarted()
        {
            if (SecurityWithSignalToClose == null)
                DoStrategyPreparation(new Security[] { }, new Security[] { Security }, new Portfolio[] { Portfolio });
            else
                DoStrategyPreparation(new Security[] { }, new Security[] { Security, SecurityWithSignalToClose }, new Portfolio[] { Portfolio });


            if (Volume <= 0 || _priceToClose <= 0) throw new ArgumentException(
                $"Volume: {Volume} or price to close: {_priceToClose} cannot be below zero"); ;

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        Stop();
                    }
                })
                .Apply(this);

            if (SecurityWithSignalToClose == null)
            {
                Security.WhenMarketDepthChanged(Connector)
                    .Do(md =>
                    {
                        var mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, _priceToClose);

                        mqs.WhenStopped()
                            .Do(this.Stop)
                            .Once()
                            .Apply(this);

                        ChildStrategies.Add(mqs);
                    })
                    .Once()
                    .Apply(this);
            }
            else
            {
                Strategy mqs = null;

                var mqsStartRule = SecurityWithSignalToClose.WhenMarketDepthChanged(Connector)
                    .Do(md =>
                    {
                        if (_securityDesirableDirection == PriceDirection.Up && md.BestBid.Price >= _priceToClose
                        || _securityDesirableDirection == PriceDirection.Down && md.BestAsk.Price <= _priceToClose)
                        {
                            // пока делаем по любой цене, как только сработает условие
                            mqs = new MarketQuoterStrategy(_strategyOrderSide, Volume, Security.GetMarketPrice(_strategyOrderSide));

                            mqs.WhenStopped()
                                .Do(this.Stop)
                                .Once()
                                .Apply(this);

                            ChildStrategies.Add(mqs);
                        }
                    })
                    .Until(() => mqs != null)
                    .Apply(this);

                this.WhenStopping()
                    .Do(() => { })
                    .Apply(this)
                    .Exclusive(mqsStartRule);
            }

            base.OnStarted();
        }
    }
}