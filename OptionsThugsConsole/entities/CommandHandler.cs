﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ecng.Collections;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;
using StockSharp.Algo;
using StockSharp.Algo.Derivatives;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Common;
using Trading.Strategies;

namespace OptionsThugsConsole.entities
{
    public class CommandHandler
    {
        public event Action<string> NewAnswer;

        private static CommandHandler Instance;

        private Dictionary<string, UserPosition> _userPositions;
        private DataManager _dataManager;
        private readonly MessageManager _messageManager;
        private readonly IConnector _connector;
        private readonly LogManager _logManager;

        private CommandHandler(IConnector connector)
        {
            _connector = connector;
            var logger = new FileLogListener
            {
                Append = true,
                MaxLength = 2048000,
                FileName = "{0:00}_{1:00}_{2:00}_log.txt".Put(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second),
                SeparateByDates = SeparateByDateModes.FileName,
                LogDirectory = "logs"
            };

            _logManager = new LogManager();

            _userPositions = new Dictionary<string, UserPosition>();

            _messageManager = new MessageManager();
            _messageManager.AutoMessage += DoAutoGeneralInfo;

            UserPosition.LoadFromXml()?.ForEach(up => { _userPositions.Add(up.SecCode, up); });

            _logManager.Listeners.Add(logger);
            _logManager.Sources.Add(_connector);
        }

        public static CommandHandler GetInstance(IConnector connector)
        {
            return Instance ?? (Instance = new CommandHandler(connector));
        }

        public void SetOutput(Action<string> outputMethod)
        {
            _messageManager.NewAnswer += outputMethod;
        }

        public void ParseUserMessage(string msg)
        {
            var tempMsgArr = msg.Split(' ');

            try
            {
                var cmd = _messageManager.ParseUserCommand(tempMsgArr[0]);
                var userArgs = _messageManager.ParseUserArgs(tempMsgArr);

                ExecuteCommand(cmd, userArgs);
            }
            catch (Exception e1)
            {
                _messageManager.ProceedAnswer(e1.Message + " Please try one of follows: "
                    + Environment.NewLine
                    + MessageManager.AlignString(Enum.GetNames(typeof(UserCommands))));

            }
        }

        private void ExecuteCommand(UserCommands cmd, string[] userParams)
        {
            try
            {
                switch (cmd)
                {
                    case UserCommands.Conn:
                        DoConnectCmd();
                        break;
                    case UserCommands.Create:
                        DoCreateCmd(userParams);
                        break;
                    case UserCommands.Start:
                        DoStartCmd(userParams);
                        break;
                    case UserCommands.Stop:
                        DoStopCmd(userParams);
                        break;
                    case UserCommands.Status:
                        DoStatusCmd();
                        break;
                    case UserCommands.Calc:
                        DoCalcCmd(userParams);
                        break;
                    case UserCommands.Settings:
                        DoSettingsCmd(userParams);
                        break;
                    case UserCommands.Dconn:
                        DoDconnCmd();
                        break;
                    case UserCommands.Timer:
                        DoTimerCmd();
                        break;
                    case UserCommands.Kill:
                        DoKillCmd(userParams);
                        break;
                }
            }
            catch (Exception e1)
            {
                _messageManager.ProceedAnswer($"unknown exception: {e1.Message}", ConsoleColor.Red);
            }
        }

        private void DoKillCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                _messageManager.ProceedAnswer("please enter correct strategy name for forcible delete", ConsoleColor.Yellow);
                return;
            }

            var strategyName = userParams[0];


            if (_dataManager?.MappedStrategies == null)
            {
                _messageManager.ProceedAnswer("some entities still null, delete impossible", ConsoleColor.Yellow);
                return;
            }

            PrimaryStrategy strategy;

            if (!_dataManager.MappedStrategies.TryGetValue(strategyName, out strategy))
            {
                _messageManager.ProceedAnswer("have no strategies with such a name.", ConsoleColor.Yellow);
                return;
            }

            if (strategy.ProcessState != ProcessStates.Stopped)
            {
                _messageManager.ProceedAnswer($"strategy must be stopped for forcible delete, current state {strategy.ProcessState}", ConsoleColor.Yellow);
                return;
            }

            _dataManager.MappedStrategies.Remove(strategyName);
            _messageManager.ProceedAnswer($"{strategyName} strategy removed forcibly from collection", ConsoleColor.Red);
        }

        private void DoTimerCmd()
        {
            if (_messageManager == null)
                return;

            if (_messageManager.IsTimerEnabled())
                _messageManager.DisableTimer();
            else
                _messageManager.EnableTimer();

            _messageManager.ProceedAnswer($"timer: {(_messageManager.IsTimerEnabled() ? "ON" : "OFF")}", ConsoleColor.Yellow);
        }

        private void DoCalcCmd(string[] userParams)
        {
            if (userParams.Length < 1 || userParams.Length > 2)
            {
                _messageManager.ProceedAnswer($"please enter type of calculation that should be done: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} " +
                            $"by option series (set middle option strike) or {UserKeyWords.Pos} for cur. position", ConsoleColor.Yellow);
                return;
            }

            UserKeyWords kw1;
            var calcType = userParams[0];
            if (!Enum.TryParse(calcType, true, out kw1))
            {
                _messageManager.ProceedAnswer($"please enter correct command: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr}", ConsoleColor.Yellow);
                return;
            }

            if (kw1 == UserKeyWords.Pos && userParams.Length != 1)
            {
                _messageManager.ProceedAnswer($"{UserKeyWords.Pos} command should be used without additional args", ConsoleColor.Yellow);
                return;
            }

            if (kw1 != UserKeyWords.Pos && userParams.Length != 2)
            {
                _messageManager.ProceedAnswer($"{UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} commands should be used with sec. code", ConsoleColor.Yellow);
                return;
            }

            var tempSecurityMap = new Dictionary<Security, decimal>();

            var sb = new StringBuilder();
            sb.AppendLine();

            if (userParams.Length == 1)
            {
                _dataManager
                    .LookupAllConnectorsPositions()
                    .ForEach(p => { tempSecurityMap.Add(p.Security, p.CurrentValue ?? 0); });
            }
            else
            {
                var secCodePart = userParams[1];

                _dataManager
                    .LookupCollectionThroughExistingSecurities(secCodePart)
                    .ForEach(s => { tempSecurityMap.Add(s, 1); });
            }

            if (tempSecurityMap.Count == 0)
            {
                _messageManager.ProceedAnswer($"any securities with such code part, please try more specific letters", ConsoleColor.Yellow);
                return;
            }

            switch (kw1)
            {
                case UserKeyWords.Pos:
                    var greeks = GetSummaryGreeks(tempSecurityMap);

                    sb.Append(MessageManager.AlignString(new string[]
                        {
                            $"dt={greeks[0]}",
                            $"gm={greeks[1]}",
                            $"vg={greeks[2]}",
                            $"th={greeks[3]}",
                        }))
                        .AppendLine();

                    sb.Append($"saved positions: ");
                    sb.AppendLine();
                    _userPositions.ForEach(kvp =>
                    {
                        sb.Append(MessageManager.AlignString(new string[]
                        {
                            kvp.Key,
                            kvp.Value.Quantity.ToString(),
                            kvp.Value.Price.ToString(),
                            kvp.Value.Money.ToString(),
                            $"created:{kvp.Value.CreatedTime}"
                        }))
                        .AppendLine();
                    });
                    sb.AppendLine();

                    break;
                case UserKeyWords.Grk:

                    var unAssetCodePart = _dataManager.UnderlyingAsset.Code.Substring(0, 2);

                    tempSecurityMap.OrderBy(kvp => kvp.Key.Code).ForEach(kvp =>
                    {
                        var curAssetCodePart = kvp.Key.Code.Substring(0, 2);

                        if (!curAssetCodePart.CompareIgnoreCase(unAssetCodePart))
                            return;

                        var secGreeks = GetSecurityGreeks(kvp.Key, kvp.Value);

                        sb.Append(MessageManager.AlignString(new string[]
                        {
                            _dataManager.GetSecurityStringRepresentation(kvp.Key),
                            $"dt={secGreeks[0]}",
                            $"gm={secGreeks[1]}",
                            $"vg={secGreeks[2]}",
                            $"th={secGreeks[3]}",
                        }))
                        .AppendLine();
                    });
                    break;
                case UserKeyWords.Spr:
                    tempSecurityMap.OrderBy(kvp => kvp.Key.Code).ForEach(kvp =>
                    {
                        sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                        .Append($" spread: {MessageManager.MsgPriceRounding(kvp.Key.BestPair?.SpreadPrice ?? 0)}" +
                                $"   [{MessageManager.MsgPriceRounding(kvp.Key.BestBid?.Price ?? 0)} / {MessageManager.MsgPriceRounding(kvp.Key.BestAsk?.Price ?? 0)}]")
                        .AppendLine();
                    });
                    break;
                case UserKeyWords.Vol:
                    tempSecurityMap.OrderBy(kvp => kvp.Key.Code).ForEach(kvp =>
                    {
                        if (kvp.Key.Type == SecurityTypes.Option)
                        {
                            var bs = new BlackScholes(kvp.Key, _dataManager.UnderlyingAsset, _connector);

                            var bidVol = kvp.Key.BestBid == null ? 0 :
                                MessageManager.MsgVolaRounding(bs.ImpliedVolatility(DateTimeOffset.Now, kvp.Key.BestBid.Price) ?? 0);
                            var askVol = kvp.Key.BestAsk == null ? 0 :
                                MessageManager.MsgVolaRounding(bs.ImpliedVolatility(DateTimeOffset.Now, kvp.Key.BestAsk.Price) ?? 0);

                            sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                            .Append($" bid vol: {bidVol}%")
                            .Append($" ask vol: {askVol}%")
                            .AppendLine();
                        }
                    });
                    break;
                default:
                    sb.Append($"please enter correct command: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} " +
                        "by option series (set middle option strike) or emptpy string for cur. positions");
                    _messageManager.ProceedAnswer(sb.ToString(), ConsoleColor.Yellow);
                    return;
            }

            _messageManager.ProceedAnswer(sb.ToString());
        }

        private void DoSettingsCmd(string[] userParams)
        {
            if (userParams.Length < 1 || userParams.Length > 2)
            {
                _messageManager.ProceedAnswer("please enter setting name and/or new setting value to change it (or keyword 'all' to show up settings)", ConsoleColor.Yellow);
                return;
            }

            var settingName = userParams[0];

            if (userParams.Length == 1)
            {

                UserKeyWords kw;

                if (Enum.TryParse(settingName, true, out kw))
                    ConfigManager.GetInstance().PrintAllSettings();
                else
                    _messageManager.ProceedAnswer(ConfigManager.GetInstance().GetSettingValue(settingName), ConsoleColor.Yellow);

            }

            if (userParams.Length == 2)
            {
                var settingValue = userParams[1];
                var oldValue = ConfigManager.GetInstance().GetSettingValue(settingName);

                ConfigManager.GetInstance().UpdateConfigFile(settingName, settingValue);

                _messageManager.ProceedAnswer($"Changed, old value: {oldValue} new value: {settingValue}");
            }
        }

        private void DoStatusCmd()
        {
            _messageManager.ProceedAnswer("", ConsoleColor.Green);
            _messageManager.ProceedAnswer($"connection: {_connector.Name}, status: {_connector.ConnectionState}", ConsoleColor.Green, false);
            _messageManager.ProceedAnswer($"loaded securities: {_connector.Securities.Count()}", ConsoleColor.Green, false);
            _messageManager.ProceedAnswer($"loaded portfolios: {_connector.Portfolios.Count()}", ConsoleColor.Green, false);

            var terminalPositionsSb = new StringBuilder();
            var xmlPosSb = new StringBuilder();

            _connector.Positions.ForEach(p =>
            {
                terminalPositionsSb.AppendLine($"{p.Portfolio.Name}    {p.CurrentValue}    {p.Security.Name}");
            });

            _userPositions.ForEach(kvp =>
            {

                xmlPosSb.Append(MessageManager.AlignString(new string[]
                    {
                        kvp.Key,
                        kvp.Value.Quantity.ToString(),
                        kvp.Value.Price.ToString(),
                        $"created:{kvp.Value.CreatedTime}"
                    }))
                    .AppendLine();
            });

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);
            _messageManager.ProceedAnswer($"terminal positions: {Environment.NewLine}{terminalPositionsSb}", ConsoleColor.Green, false);
            _messageManager.ProceedAnswer($"file positions: {Environment.NewLine}{xmlPosSb}", ConsoleColor.Yellow, false);

            if (_dataManager.MappedStrategies.Count == 0)
            {
                _messageManager.ProceedAnswer("still no strategies created.", ConsoleColor.Green, false);
            }
            else
            {
                var sb = new StringBuilder();
                sb
                .Append("strategies: ")
                .AppendLine();

                _dataManager.MappedStrategies.ForEach(kvp =>
                {
                    sb.Append(MessageManager.AlignString(new string[]
                    {
                        "name: ",
                        kvp.Key,
                        "type:",
                        kvp.Value.GetType().Name,
                        "state:",
                        kvp.Value.ProcessState.ToString(),
                        "errors:",
                        kvp.Value.ErrorCount.ToString(),
                        "position:",
                        kvp.Value.Position.ToString()
                    }, true, 2));

                    sb.Append("STRRPS:  ")
                    .Append(kvp.Value.ToString());

                    sb.AppendLine()
                    .AppendLine("***")
                    .AppendLine();
                });
                _messageManager.ProceedAnswer(sb.ToString(), ConsoleColor.Green, false);
            }
            _messageManager.ProceedAnswer($"program underlying asset: {_dataManager.UnderlyingAsset.Code} ({_dataManager.GetSecurityStringRepresentation(_dataManager.UnderlyingAsset)})", ConsoleColor.Green, false);
        }

        private void DoStopCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                _messageManager.ProceedAnswer("please enter name one of existing strategies  to start (or keyword 'all')", ConsoleColor.Yellow);
                return;
            }

            if (_dataManager.MappedStrategies.Count == 0)
            {
                _messageManager.ProceedAnswer("have no strategies to stop", ConsoleColor.Yellow);
                return;
            }

            string strategyName = userParams[0];

            UserKeyWords kw;

            if (Enum.TryParse(strategyName, true, out kw))
            {
                if (kw == UserKeyWords.All)
                    _dataManager.MappedStrategies.ForEach(kvp =>
                    {
                        Task.Run(() => TryToStopStrategy(kvp.Key, kvp.Value)); // TODO проверить, может стоп метод вылетает и остальные не исполняются. попробую в отд потоке.
                    });
            }
            else
            {
                if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                    TryToStopStrategy(strategyName, _dataManager.MappedStrategies[strategyName]);

                else
                    _messageManager.ProceedAnswer("please choose correct strategy name from following: "
                        + MessageManager.AlignString(_dataManager.MappedStrategies.Select(kvp => kvp.Key).ToArray()), ConsoleColor.Yellow);
            }
        }

        private void DoStartCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                _messageManager.ProceedAnswer("please enter one name of strategy to start (or keyword 'all')", ConsoleColor.Yellow);
                return;
            }

            if (_dataManager.MappedStrategies.Count == 0)
            {
                _messageManager.ProceedAnswer("have no strategies to start", ConsoleColor.Yellow);
                return;
            }

            string strategyName = userParams[0];

            UserKeyWords kw;

            if (Enum.TryParse(strategyName, true, out kw))
            {
                if (kw == UserKeyWords.All)
                    _dataManager.MappedStrategies.ForEach(kvp =>
                    {
                        AssignCommonStrategyRules(kvp.Key, kvp.Value);

                        kvp.Value.Start();
                    });
            }
            else
            {
                if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                {
                    var soughtStrategy = _dataManager.MappedStrategies[strategyName];

                    AssignCommonStrategyRules(strategyName, soughtStrategy);

                    soughtStrategy.Start();
                }
                else
                {
                    _messageManager.ProceedAnswer("please choose correct strategy name from following: "
                        + MessageManager.AlignString(_dataManager.MappedStrategies.Select(kvp => kvp.Key).ToArray()), ConsoleColor.Yellow);
                }
            }
        }

        private void DoCreateCmd(string[] userParams)
        {
            if (userParams.Length < 2)
            {
                _messageManager.ProceedAnswer("please enter type and name of created strategy.", ConsoleColor.Yellow);
                return;
            }

            StrategyTypes param1;
            string param2 = userParams[1];

            if (param2.Length < 5
                || Char.IsNumber(param2.ToCharArray()[0]))
            {
                _messageManager.ProceedAnswer("strategy name should have length more that 5 symbols and started from letter (NOT number)", ConsoleColor.Yellow);
                return;
            }

            if (!Enum.TryParse(userParams[0], true, out param1))
            {
                _messageManager.ProceedAnswer($"cannot create such a strategy. Possible types: " +
                            $"{MessageManager.AlignString(Enum.GetNames(typeof(StrategyTypes)))}", ConsoleColor.Yellow);
                return;
            }

            if (_dataManager.MappedStrategies.ContainsKey(param2))
            {
                _messageManager.ProceedAnswer("strategy with such name has already exist.", ConsoleColor.Yellow);
                return;
            }

            StrategyManager strategyMaker = new StrategyManager(_connector, _dataManager);

            _messageManager.ProceedAnswer(strategyMaker.GetStrategyStringLayout(param1));

            try
            {
                _messageManager.ProceedAnswer(Environment.NewLine +
                            "Please enter straregy params at the next line like in format above AND:" +
                            Environment.NewLine +
                            "-main separator is semicolon WITHOUT space" + Environment.NewLine +
                            "-after last value DO NOT USE semicolon" + Environment.NewLine +
                            "-USE spaces like a separator in array []", ConsoleColor.Yellow);
                var strategy = strategyMaker.CreateStrategyFromString(param1, Console.ReadLine());
                _dataManager.MappedStrategies.Add(param2, strategy);

                _logManager.Sources.Add(strategy);

                _messageManager.ProceedAnswer("strategy created.");
            }
            catch (ArgumentException e1)
            {
                _messageManager.ProceedAnswer(e1.Message, ConsoleColor.Yellow);
            }
        }

        private void DoConnectCmd()
        {
            if (_connector.ConnectionState == ConnectionStates.Connected)
            {
                _messageManager.ProceedAnswer("already connected.");
                return;
            }

            _connector.Connected += () =>
            {
                _messageManager.ProceedAnswer("connected (success), loading securities, pls wait...");
                _dataManager = new DataManager(_connector);
            };

            _connector?.Connect();

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(5000); //dunno what to do

                    var tempSecurityMap = new SynchronizedDictionary<string, Security>();
                    var cannotReadCounter = 0;
                    var alreadyLoadedCounter = 0;

                    _connector.Securities.ForEach(s =>
                    {
                        if (s.ExpiryDate == null || s.Type != SecurityTypes.Future && s.Type != SecurityTypes.Option)
                        {
                            cannotReadCounter++;
                            return;
                        }

                        var key = _dataManager.GetSecurityStringRepresentation(s);

                        if (tempSecurityMap.ContainsKey(key))
                        {
                            alreadyLoadedCounter++;
                        }
                        else
                        {
                            tempSecurityMap.Add(key, s);
                        }
                    });

                    _dataManager.MappedSecurities = tempSecurityMap;

                    _messageManager.ProceedAnswer($"couldn't read instruments: {cannotReadCounter}", ConsoleColor.Red, false);
                    _messageManager.ProceedAnswer($"attempts to load more than twice: {alreadyLoadedCounter}", ConsoleColor.Red, false);

                    _dataManager.UnderlyingAsset = _dataManager.LookupThroughExistingSecurities(
                        ConfigManager.GetInstance().GetSettingValue(UserConfigs.Asset.ToString()));

                    _dataManager.RegisterMappedUndAssetsSecuruties();

                    _messageManager.ProceedAnswer($"{_dataManager.MappedSecurities.Count} securities loaded.");

                    _connector.NewMyTrade += mt =>
                    {
                        var secCode = mt.Trade.Security.Code;
                        var price = mt.Trade.Price;
                        var size = mt.Trade.Volume;
                        var side = mt.Order.Direction;

                        if (!_userPositions.ContainsKey(secCode))
                        {
                            var userPosNew = new UserPosition(secCode);
                            _userPositions.Add(secCode, userPosNew);

                        }

                        _userPositions[secCode].AddNewDeal(side, price, size);

                        UserPosition.SaveToXml(_userPositions.Values.ToList());

                        _messageManager.ProceedAnswer(MessageManager.AlignString(new string[]
                        {
                            "NEW TRADE [",
                            $"side: {mt.Order.Direction}",
                            $"size: {mt.Trade.Volume}",
                            $"price: {mt.Trade.Price}",
                            $"sec: {mt.Trade.Security.Code}",
                            " ], deal saved"
                        }), ConsoleColor.Magenta);
                    };

                    _messageManager.ProceedAnswer("Print 'timer' to enable/disable auto-print-information about positions.", ConsoleColor.Yellow);
                    DoStatusCmd();


                }
                catch (Exception e1)
                {
                    _messageManager.ProceedAnswer($"unknown exception: {e1.Message}", ConsoleColor.Red);
                }
            });
        }

        private void DoDconnCmd()
        {
            if (_connector.ConnectionState != ConnectionStates.Connected)
            {
                _messageManager.ProceedAnswer("have no connection to break.");
                return;
            }

            _connector.Disconnected += () =>
            {
                _messageManager.ProceedAnswer("disconnected (success)");
            };

            DoStopCmd(new[] { UserKeyWords.All.ToString() });

            _messageManager.DisableTimer();

            _connector?.Disconnect();
        }

        private void AssignCommonStrategyRules(string name, PrimaryStrategy strategy)
        {
            strategy.WhenStarted()
                        .Do(() =>
                {
                    _messageManager.ProceedAnswer("");
                    _messageManager.ProceedAnswer($"{strategy} strategy started (key {name}).");
                })
                        .Apply(strategy);

            strategy.WhenStopping()
                .Do(() =>
                {
                    _messageManager.ProceedAnswer("");
                    _messageManager.ProceedAnswer($"{name} strategy STOPPING, pos: {strategy.Position}");
                })
                .Apply(strategy);

            strategy.PrimaryStrategyStopped += () =>
            {
                {
                    _dataManager.MappedStrategies.Remove(name);
                    _messageManager.ProceedAnswer("");
                    _messageManager.ProceedAnswer($"{name} strategy STOPPED and removed from collection");
                }
            };
        }

        private void TryToStopStrategy(string name, PrimaryStrategy strategy)
        {
            if (strategy.ProcessState == ProcessStates.Started)
                strategy.PrimaryStopping();
            else
                _messageManager.ProceedAnswer($"such a strategy cannot be stopped: {name} {strategy.ProcessState}", ConsoleColor.Red, false);
        }

        private decimal[] GetSummaryGreeks(Dictionary<Security, decimal> securityPositionMapping)
        {
            decimal delta = 0M;
            decimal gamma = 0M;
            decimal vega = 0M;
            decimal theta = 0M;

            securityPositionMapping.OrderBy(kvp => kvp.Key.Code).ForEach(kvp =>
            {
                if (kvp.Value == 0)
                {
                    return;
                }

                if (kvp.Key.Type == SecurityTypes.Future)
                {
                    delta += kvp.Value;
                }

                if (kvp.Key.Type == SecurityTypes.Option)
                {
                    var bs = new BlackScholes(kvp.Key, _dataManager.UnderlyingAsset, _connector);
                    var lastPrice = kvp.Value > 0
                        ? _dataManager.UnderlyingAsset.BestBid.Price
                        : _dataManager.UnderlyingAsset.BestAsk.Price;

                    delta += MessageManager.MsgGreeksRounding((bs.Delta(DateTimeOffset.Now, null, lastPrice) ?? 0)) * kvp.Value;
                    gamma += MessageManager.MsgGreeksRounding((bs.Gamma(DateTimeOffset.Now, null, lastPrice) ?? 0), 1) * kvp.Value;
                    vega += MessageManager.MsgGreeksRounding((bs.Vega(DateTimeOffset.Now, null, lastPrice) ?? 0)) * kvp.Value;
                    theta += MessageManager.MsgGreeksRounding((bs.Theta(DateTimeOffset.Now, null, lastPrice) ?? 0), -2) * kvp.Value;
                }
            });

            return new decimal[] { delta, gamma, vega, theta };
        }

        private decimal[] GetSecurityGreeks(Security sec, decimal pos)
        {
            decimal delta = 0M;
            decimal gamma = 0M;
            decimal vega = 0M;
            decimal theta = 0M;

            if (sec.Type == SecurityTypes.Future)
            {
                delta += pos;
            }

            if (sec.Type == SecurityTypes.Option)
            {
                var bs = new BlackScholes(sec, _dataManager.UnderlyingAsset, _connector);
                var lastPrice = pos > 0
                    ? _dataManager.UnderlyingAsset.BestBid.Price
                    : _dataManager.UnderlyingAsset.BestAsk.Price;

                delta = MessageManager.MsgGreeksRounding(bs.Delta(DateTimeOffset.Now, null, lastPrice) ?? 0);
                gamma = MessageManager.MsgGreeksRounding(bs.Gamma(DateTimeOffset.Now, null, lastPrice) ?? 0, 1);
                vega = MessageManager.MsgGreeksRounding(bs.Vega(DateTimeOffset.Now, null, lastPrice) ?? 0);
                theta = MessageManager.MsgGreeksRounding(bs.Theta(DateTimeOffset.Now, null, lastPrice) ?? 0, -2);
            }

            return new decimal[] { delta, gamma, vega, theta };

        }

        private void DoAutoGeneralInfo()
        {
            _messageManager.ProceedAnswer("-----------------------------------", ConsoleColor.Magenta);

            _messageManager.ProceedAnswer(MessageManager.AlignString(new string[]
            {
                "Connection state:",
                _connector?.ConnectionState.ToString()
            }), ConsoleColor.Green, false);

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);

            var sb = new StringBuilder();

            sb.Append("terminal positions:").Append(" ");

            _connector?.Positions?.ForEach(p =>
            {
                sb.Append(p.Security.Code).Append(" ").Append(p.CurrentValue).Append(" ");
            });

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);

            _messageManager.ProceedAnswer(MessageManager.AlignString(sb.ToString().Split(" "), true, 2, " "), ConsoleColor.Green, false);

            sb.Clear();

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);

            sb.Append("xml-file positions:").Append(" ");

            _userPositions.ForEach(kvp =>
            {
                sb.Append(kvp.Key).Append(" ").Append(kvp.Value.Quantity).Append(" ");

            });

            _messageManager.ProceedAnswer(MessageManager.AlignString(sb.ToString().Split(" "), true, 2, " "), ConsoleColor.Green, false);

            sb.Clear();

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);

            sb.Append("str-gies positions :").Append(" ");
            _dataManager?.MappedStrategies?.ForEach(kvp =>
            {
                sb.Append(kvp.Key).Append(" ").Append(kvp.Value.Position).Append(" ").Append(kvp.Value.ProcessState).Append(" ");

            });

            _messageManager.ProceedAnswer(MessageManager.AlignString(sb.ToString().Split(" "), true, 3, " "), ConsoleColor.Green, false);

            _messageManager.ProceedAnswer("", ConsoleColor.Green, false);

            var tempSecurityMap = new Dictionary<Security, decimal>();

            _dataManager
                .LookupAllConnectorsPositions()
                .ForEach(p => { tempSecurityMap.Add(p.Security, p.CurrentValue ?? 0); });

            var greeks = GetSummaryGreeks(tempSecurityMap);

            _messageManager.ProceedAnswer($"delta: {greeks[0]} gamma: {greeks[1]} vega: {greeks[2]} theta: {greeks[3]}", ConsoleColor.Green, false);

            _messageManager.ProceedAnswer("-----------------------------------", ConsoleColor.Magenta);
        }
    }
}
