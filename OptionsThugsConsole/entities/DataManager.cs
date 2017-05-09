﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ecng.Collections;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Strategies;

namespace OptionsThugsConsole.entities
{
    public class DataManager
    {
        private readonly IConnector _connector;

        public Dictionary<string, PrimaryStrategy> MappedStrategies { get; }
        public SynchronizedDictionary<string, Security> MappedSecurities { get; }

        public DataManager(IConnector connector)
        {
            MappedStrategies = new Dictionary<string, PrimaryStrategy>();
            MappedSecurities = new SynchronizedDictionary<string, Security>();

            _connector = connector;

            _connector.NewSecurity += s => MappedSecurities.Add(GetSecurityStringRepresentation(s), s);
        }

        public Security LookupThroughExistingSecurities(string secCodePart)
        {
            Security tempSec = null;
            List<Security> tempSecurities = new List<Security>();

            MappedSecurities.Keys.ForEach(key =>
            {
                if (key.ToLower().Contains(secCodePart.ToLower()))
                {
                    tempSec = MappedSecurities[key];
                    tempSecurities.Add(MappedSecurities[key]);

                }
            });

            if (tempSecurities.Count > 1)
                throw new ArgumentException("more than one matches in collection. Please, enter more specific security code from follows: "
                    + Environment.NewLine
                    + tempSecurities.Select(s => s.Code + " " + s.Type + " " + s.OptionType + " " +
                                                 $"{s.ExpiryDate:dd.MM.yyyy}").ToArray().Join(Environment.NewLine));

            if (tempSec == null)
                throw new ArgumentException("have no matches for such an instrument, please enter correct security code.");


            return tempSec;
        }

        public Portfolio LookupThroughConnectorsPortfolios(string portfolioNamePart)
        {
            Portfolio tempPortfolio = null;
            List<Portfolio> tempPortfolios = new List<Portfolio>();

            _connector.Portfolios.ForEach(p =>
            {
                if (p.Name.ToLower().Contains(portfolioNamePart.ToLower()))
                {
                    tempPortfolio = p;
                    tempPortfolios.Add(p);

                }
            });

            if (tempPortfolios.Count > 1)
                throw new ArgumentException("more than one matches in collection. Please, enter more specific portfolio name from follows: "
                    + Environment.NewLine
                    + tempPortfolios.Select(p => p.Name + " " + p.Board + " " + p.State).ToArray().Join(Environment.NewLine));

            if (tempPortfolios == null)
                throw new ArgumentException("have no matches for such an instrument, please enter correct portfolio name.");


            return tempPortfolio;
        }

        private string GetSecurityStringRepresentation(Security s)
        {
            var sb = new StringBuilder();
            var code = s.Code.Substring(0, 2);

            if (s.ExpiryDate == null)
                throw new NullReferenceException("expire date of instrument is null");

            if (s.Type == SecurityTypes.Future)
                return sb
                    .Append(s.ExpiryDate.Value.Month)
                    .Append(code)
                    .Append(UserKeyWords.F.GetName())
                    .ToString();

            if (s.OptionType == OptionTypes.Call)
                return sb
                    .Append(s.ExpiryDate.Value.Month)
                    .Append(code)
                    .Append(UserKeyWords.C.GetName())
                    .Append(s.Strike)
                    .ToString();

            if (s.OptionType == OptionTypes.Put)
                return sb
                    .Append(s.ExpiryDate.Value.Month)
                    .Append(code)
                    .Append(UserKeyWords.P.GetName())
                    .Append(s.Strike)
                    .ToString();

            throw new ArgumentException("could be only parsed futures and options");
        }
    }
}