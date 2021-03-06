﻿using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Strategies;

namespace OptionsThugs.xTests
{
    public class MqsTest : BaseStrategyTest
    {
        public MqsTest(LogManager logManager, Connector stConnector, Portfolio stPortfolio, Security sSecurity) : base(logManager, stConnector, stPortfolio, sSecurity)
        {
        }

        public void CreateNewMqstrategy(Sides side, decimal volume, decimal targetPrice)
        {
            StrategyForTest = new MarketQuoterStrategy(side, volume, targetPrice);

            StrategyForTest.SetStrategyEntitiesForWork(StConnector, StSecurity, StPortfolio);
        }
    }
}
