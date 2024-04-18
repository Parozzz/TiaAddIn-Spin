﻿using System;
using System.Collections.Generic;
using System.IO;
using TiaXmlReader.SimaticML.Enums;
using TiaXmlReader.Utility;
using TiaXmlReader.SimaticML;
using TiaXmlReader.SimaticML.Blocks;
using TiaXmlReader.SimaticML.Blocks.FlagNet.nAccess;
using TiaXmlReader.SimaticML.Blocks.FlagNet.nPart;

namespace TiaXmlReader.Generation.UserAlarms
{

    public class AlarmXmlGeneration
    {
        private readonly AlarmConfiguration config;
        private readonly List<AlarmData> alarmDataList;
        private readonly List<DeviceData> deviceDataList;

        private BlockFC fc;
        private CompileUnit compileUnit;
        private string fullAlarmList;

        public AlarmXmlGeneration(AlarmConfiguration config, List<AlarmData> alarmDataList, List<DeviceData> deviceDataList)
        {
            this.config = config;
            this.alarmDataList = alarmDataList;
            this.deviceDataList = deviceDataList;
        }

        public void GenerateBlocks()
        {
            fc = new BlockFC();
            fc.Init();
            fc.GetBlockAttributes().SetBlockName(config.FCBlockName).SetBlockNumber(config.FCBlockNumber).SetAutoNumber(config.FCBlockNumber > 0);

            fullAlarmList = "";

            var nextAlarmNum = config.StartingAlarmNum;
            switch (config.PartitionType)
            {
                case AlarmPartitionType.DEVICE:
                    foreach (var deviceData in deviceDataList)
                    {
                        if (config.GroupingType == AlarmGroupingType.GROUP)
                        {
                            compileUnit = fc.AddCompileUnit();
                            compileUnit.Init();
                        }

                        var placeholders = new GenerationPlaceholders();

                        var startAlarmNum = nextAlarmNum;
                        foreach (var alarmData in alarmDataList)
                        {
                            if (!alarmData.Enable)
                            {
                                continue;
                            }

                            var parsedAlarmData = ReplaceAlarmDataWithDefaultAndPrefix(alarmData);

                            placeholders.SetConsumerData(deviceData)
                                .SetAlarmData(parsedAlarmData)
                                .SetAlarmNum(nextAlarmNum++, config.AlarmNumFormat);
                            fullAlarmList += placeholders.Parse(GenerationPlaceholders.ALARM_DESCRIPTION) + '\n';

                            if (config.GroupingType == AlarmGroupingType.ONE)
                            {
                                compileUnit = fc.AddCompileUnit();
                                compileUnit.Init();
                                compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.OneEachSegmentName));
                            }

                            FillAlarmCompileUnit(compileUnit, placeholders, parsedAlarmData);
                        }

                        var lastAlarmNum = nextAlarmNum - 1;
                        var alarmCount = (lastAlarmNum - startAlarmNum) + 1;

                        var slippingAlarmCount = config.AntiSlipNumber % alarmCount;
                        if (config.AntiSlipNumber > 0 && slippingAlarmCount > 0)
                        {
                            nextAlarmNum += slippingAlarmCount;

                            if (config.GenerateEmptyAlarmAntiSlip)
                            {
                                GenerateEmptyAlarms(config.GroupingType, config.PartitionType, lastAlarmNum + 1, slippingAlarmCount, compileUnit); //CompileUnit only used for group division
                                lastAlarmNum += slippingAlarmCount;
                            }
                        }

                        if (config.GroupingType == AlarmGroupingType.GROUP)
                        {
                            placeholders.SetStartEndAlarmNum(startAlarmNum, lastAlarmNum, config.AlarmNumFormat);
                            compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.GroupSegmentName));
                        }

                        nextAlarmNum += config.SkipNumberAfterGroup;
                    }
                    break;
                case AlarmPartitionType.ALARM_TYPE:
                    foreach (var alarmData in alarmDataList)
                    {
                        if (!alarmData.Enable)
                        {
                            continue;
                        }

                        var parsedAlarmData = ReplaceAlarmDataWithDefaultAndPrefix(alarmData);

                        if (config.GroupingType == AlarmGroupingType.GROUP)
                        {
                            compileUnit = fc.AddCompileUnit();
                            compileUnit.Init();
                        }

                        var placeholders = new GenerationPlaceholders();

                        var startAlarmNum = nextAlarmNum;
                        foreach (var deviceData in deviceDataList)
                        {
                            placeholders.SetConsumerData(deviceData)
                                    .SetAlarmData(parsedAlarmData)
                                    .SetAlarmNum(nextAlarmNum++, config.AlarmNumFormat);
                            fullAlarmList += placeholders.Parse(GenerationPlaceholders.ALARM_DESCRIPTION) + '\n';

                            if (config.GroupingType == AlarmGroupingType.ONE)
                            {
                                compileUnit = fc.AddCompileUnit();
                                compileUnit.Init();
                                compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.OneEachSegmentName));
                            }

                            FillAlarmCompileUnit(compileUnit, placeholders, parsedAlarmData);
                        }

                        var lastAlarmNum = nextAlarmNum - 1;
                        var alarmCount = (lastAlarmNum - startAlarmNum) + 1;

                        var slippingAlarmCount = config.AntiSlipNumber % alarmCount;
                        if (config.AntiSlipNumber > 0 && slippingAlarmCount > 0)
                        {
                            nextAlarmNum += slippingAlarmCount;

                            if (config.GenerateEmptyAlarmAntiSlip)
                            {
                                GenerateEmptyAlarms(config.GroupingType, config.PartitionType, lastAlarmNum + 1, slippingAlarmCount, compileUnit); //CompileUnit only used for group division
                                lastAlarmNum += slippingAlarmCount;
                            }
                        }

                        if (config.GroupingType == AlarmGroupingType.GROUP)
                        {
                            placeholders.SetStartEndAlarmNum(startAlarmNum, lastAlarmNum, config.AlarmNumFormat);
                            compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.GroupSegmentName));
                        }

                        nextAlarmNum += config.SkipNumberAfterGroup;
                    }
                    break;
            }

            GenerateEmptyAlarms(config.GroupingType, config.PartitionType, nextAlarmNum, config.EmptyAlarmAtEnd);
        }

        private void GenerateEmptyAlarms(AlarmGroupingType groupingType, AlarmPartitionType partitionType, uint startAlarmNum, uint alarmCount, CompileUnit externalGroupCompileUnit = null)
        {
            var emptyAlarmData = new AlarmData()
            {
                AlarmAddress = config.EmptyAlarmContactAddress,
                CoilAddress = config.DefaultCoilAddress,
                SetCoilAddress = config.DefaultSetCoilAddress,
                TimerAddress = config.DefaultTimerAddress,
                TimerType = config.DefaultTimerType,
                TimerValue = config.DefaultTimerValue,
                Description = "",
                Enable = true
            };

            var alarmNum = startAlarmNum;

            CompileUnit compileUnit = externalGroupCompileUnit;
            if (compileUnit == null && groupingType == AlarmGroupingType.GROUP)
            {
                var placeholders = new GenerationPlaceholders()
                    .SetAlarmData(emptyAlarmData)
                    .SetStartEndAlarmNum(alarmNum, alarmNum + (alarmCount - 1), config.AlarmNumFormat);

                compileUnit = fc.AddCompileUnit();
                compileUnit.Init();
                compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.GroupEmptyAlarmSegmentName));
            }

            for (int j = 0; j < alarmCount; j++)
            {
                var placeholders = new GenerationPlaceholders().SetAlarmData(emptyAlarmData).SetAlarmNum(alarmNum++, config.AlarmNumFormat);

                if (groupingType == AlarmGroupingType.ONE)
                {
                    compileUnit = fc.AddCompileUnit();
                    compileUnit.Init();
                    compileUnit.ComputeBlockTitle().SetText(SystemVariables.CULTURE, placeholders.Parse(config.OneEachEmptyAlarmSegmentName));
                }

                FillAlarmCompileUnit(compileUnit, placeholders, emptyAlarmData);
            }
        }

        private AlarmData ReplaceAlarmDataWithDefaultAndPrefix(AlarmData alarmData)
        {
            return new AlarmData()
            {
                AlarmAddress = config.AlarmAddressPrefix + alarmData.AlarmAddress,
                CoilAddress = string.IsNullOrEmpty(alarmData.CoilAddress) ? config.DefaultCoilAddress : (config.CoilAddressPrefix + alarmData.CoilAddress),
                SetCoilAddress = string.IsNullOrEmpty(alarmData.SetCoilAddress) ? config.DefaultSetCoilAddress : (config.SetCoilAddressPrefix + alarmData.SetCoilAddress),
                TimerAddress = string.IsNullOrEmpty(alarmData.TimerAddress) ? config.DefaultTimerAddress : (config.TimerAddressPrefix + alarmData.TimerAddress),
                TimerType = Utils.StringFullOr(alarmData.TimerType, config.DefaultTimerType),
                TimerValue = Utils.StringFullOr(alarmData.TimerValue, config.DefaultTimerValue),
                Description = alarmData.Description,
                Enable = alarmData.Enable
            };

        }

        private void FillAlarmCompileUnit(CompileUnit compileUnit, GenerationPlaceholders placeholders, AlarmData alarmData)
        {
            var parsedContactAddress = placeholders.Parse(alarmData.AlarmAddress);

            IAccessData contactAccessData;
            switch (parsedContactAddress.ToLower())
            {
                case "false":
                    contactAccessData = LiteralConstantAccessData.Create(compileUnit, SimaticDataType.BOOLEAN, "FALSE");
                    break;
                case "0":
                    contactAccessData = LiteralConstantAccessData.Create(compileUnit, SimaticDataType.BOOLEAN, "0");
                    break;
                case "true":
                    contactAccessData = LiteralConstantAccessData.Create(compileUnit, SimaticDataType.BOOLEAN, "TRUE");
                    break;
                case "1":
                    contactAccessData = LiteralConstantAccessData.Create(compileUnit, SimaticDataType.BOOLEAN, "1");
                    break;
                default:
                    contactAccessData = GlobalVariableAccessData.Create(compileUnit, parsedContactAddress);
                    break;
            }

            var contact = new ContactPartData(compileUnit);
            contact.CreateIdentWire(contactAccessData);
            contact.CreatePowerrailConnection();

            IPartData timer = null;
            if (!string.IsNullOrEmpty(alarmData.TimerAddress) && alarmData.TimerAddress.ToLower() != "\\")
            {
                PartType partType;
                switch (alarmData.TimerType.ToLower())
                {
                    case "ton":
                        partType = PartType.TON;
                        break;
                    case "tof":
                        partType = PartType.TOF;
                        break;
                    default:
                        throw new Exception("Unknow timer type of " + alarmData.TimerType);
                }

                timer = new TimerPartData(compileUnit, partType);
                ((TimerPartData)timer)
                    .SetPartInstance(SimaticVariableScope.GLOBAL_VARIABLE, placeholders.Parse(alarmData.TimerAddress))
                    .SetTimeValue(alarmData.TimerValue);
            }

            IPartData setCoil = null;
            if (alarmData.SetCoilAddress.ToLower() != "\\")
            {
                setCoil = new SetCoilPartData(compileUnit);
                ((SetCoilPartData)setCoil).CreateIdentWire(GlobalVariableAccessData.Create(compileUnit, placeholders.Parse(alarmData.SetCoilAddress)));
            }

            IPartData coil = null;
            if (alarmData.CoilAddress.ToLower() != "\\")
            {
                coil = new CoilPartData(compileUnit);
                ((CoilPartData)coil).CreateIdentWire(GlobalVariableAccessData.Create(compileUnit, placeholders.Parse(alarmData.CoilAddress)));
            }

            var partDataList = new List<IPartData>
            {
                contact,
                timer,
                config.CoilFirst ? coil : setCoil,
                config.CoilFirst ? setCoil : coil
            };

            IPartData previousPartData = null;
            foreach (var partData in partDataList)
            {
                if (previousPartData == null)
                {
                    previousPartData = partData;
                    continue;
                }

                if (partData != null)
                {
                    previousPartData.CreateOutputConnection(previousPartData = partData);
                }
            }

            if (previousPartData == contact)
            {
                contact.CreateOpenCon();
            }
        }

        public void ExportXML(string exportPath)
        {
            if (string.IsNullOrEmpty(exportPath))
            {
                return;
            }

            if (fc == null)
            {
                throw new ArgumentNullException("Blocks has not been generated");
            }

            var xmlDocument = SimaticMLParser.CreateDocument();
            xmlDocument.DocumentElement.AppendChild(fc.Generate(xmlDocument, new IDGenerator()));
            xmlDocument.Save(exportPath + "/fcExport_" + fc.GetBlockAttributes().GetBlockName() + ".xml");

            var alarmTextPath = exportPath + "/alarmsText.txt";
            using (var stream = File.CreateText(alarmTextPath))
            {
                stream.Write(fullAlarmList);
            }
        }

    }
}