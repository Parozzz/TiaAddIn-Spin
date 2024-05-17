﻿using System;
using System.Windows.Forms;
using TiaUtilities.Generation.Configuration.Utility;
using TiaXmlReader.Generation.Configuration;
using TiaXmlReader.Generation.GridHandler;
using TiaXmlReader.Languages;

namespace TiaXmlReader.Generation.Alarms.GenerationForm
{
    public class AlarmGenerationFormConfigHandler
    {
        private readonly AlarmGenerationForm form;
        private readonly AlarmConfiguration config;

        public AlarmGenerationFormConfigHandler(AlarmGenerationForm form, AlarmConfiguration config)
        {
            this.form = form;
            this.config = config;
        }

        public void Init()
        {
            {
                var comboBox = form.partitionTypeComboBox;
                comboBox.SelectedValue = config.PartitionType;
                comboBox.SelectionChangeCommitted += (sender, args) => config.PartitionType = (AlarmPartitionType)(comboBox.SelectedValue ?? default(AlarmPartitionType));
            }

            {
                var comboBox = form.groupingTypeComboBox;
                comboBox.SelectedValue = config.GroupingType;
                comboBox.SelectionChangeCommitted += (sender, args) => config.GroupingType = (AlarmGroupingType)(comboBox.SelectedValue ?? default(AlarmGroupingType));
            }

            {
                var button = form.fcConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text);

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("GENERICS_NAME")
                        .ControlText(config.FCBlockName)
                        .TextChanged(v => config.FCBlockName = v);

                    mainGroup.AddTextBox().LocalizedLabel("GENERICS_NUMBER")
                        .ControlText(config.FCBlockNumber)
                        .UIntChanged(v => config.FCBlockNumber = v);

                    mainGroup.AddCheckBox().LocalizedLabel("ALARM_CONFIG_FC_COIL_FIRST")
                        .Value(config.CoilFirst)
                        .CheckedChanged(v => config.CoilFirst = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.alarmGenerationConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text);

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_START_NUM")
                         .ControlText(config.StartingAlarmNum)
                         .UIntChanged(v => config.StartingAlarmNum = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_FORMAT")
                         .ControlText(config.AlarmNumFormat)
                         .TextChanged(v => config.AlarmNumFormat = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_ANTI_SLIP")
                         .ControlText(config.AntiSlipNumber)
                         .UIntChanged(v => config.AntiSlipNumber = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_SKIP")
                         .ControlText(config.SkipNumberAfterGroup)
                         .UIntChanged(v => config.SkipNumberAfterGroup = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.emptyAlarmGenerationConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text) { ControlWidth = 200 };

                    var mainGroup = configForm.Init();
                    mainGroup.AddCheckBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_ANTI_SLIP")
                         .Value(config.GenerateEmptyAlarmAntiSlip)
                         .CheckedChanged(v => config.GenerateEmptyAlarmAntiSlip = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_NUM")
                         .ControlText(config.EmptyAlarmAtEnd)
                         .UIntChanged(v => config.EmptyAlarmAtEnd = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_ALARM_ADDRESS")
                         .ControlText(config.EmptyAlarmContactAddress)
                         .TextChanged(v => config.EmptyAlarmContactAddress = v);


                    var timerGroup = mainGroup.AddGroup().ControlWidth(225).NoAdapt();

                    timerGroup.AddLabel().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_TIMER");

                    timerGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_TIMER_ADDRESS")
                         .ControlText(config.EmptyAlarmTimerAddress)
                         .TextChanged(v => config.EmptyAlarmTimerAddress = v);

                    timerGroup.AddComboBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_TIMER_TYPE")
                         .Items(["TON", "TOF"]).DisableEdit()
                         .ControlText(config.EmptyAlarmTimerType)
                         .TextChanged(v => config.EmptyAlarmTimerType = v);

                    timerGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_GENERATION_EMPTY_TIMER_VALUE")
                         .ControlText(config.EmptyAlarmTimerValue)
                         .TextChanged(v => config.EmptyAlarmTimerValue = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.fieldDefaultValueConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text);

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_DEFAULTS_COIL")
                         .ControlText(config.DefaultCoilAddress)
                         .TextChanged(v => config.DefaultCoilAddress = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_DEFAULTS_SET_COIL")
                         .ControlText(config.DefaultSetCoilAddress)
                         .TextChanged(v => config.DefaultSetCoilAddress = v);

                    var timerGroup = mainGroup.AddGroup().ControlWidth(225).NoAdapt();

                    timerGroup.AddLabel().LocalizedLabel("ALARM_CONFIG_DEFAULTS_TIMER");

                    timerGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_DEFAULTS_TIMER_ADDRESS")
                         .ControlText(config.DefaultTimerAddress)
                         .TextChanged(v => config.DefaultTimerAddress = v);

                    timerGroup.AddComboBox().LocalizedLabel("ALARM_CONFIG_DEFAULTS_TIMER_TYPE")
                         .Items(["TON", "TOF"]).DisableEdit()
                         .ControlText(config.DefaultTimerType)
                         .TextChanged(v => config.DefaultTimerType = v);

                    timerGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_DEFAULTS_TIMER_VALUE")
                         .ControlText(config.DefaultTimerValue)
                         .TextChanged(v => config.DefaultTimerValue = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.fieldPrefixConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text);

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_PREFIX_ALARM_ADDRESS")
                         .ControlText(config.AlarmAddressPrefix)
                         .TextChanged(v => config.AlarmAddressPrefix = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_PREFIX_COIL_ADDRESS")
                         .ControlText(config.CoilAddressPrefix)
                         .TextChanged(v => config.CoilAddressPrefix = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_PREFIX_SET_COIL_ADDRESS")
                         .ControlText(config.SetCoilAddressPrefix)
                         .TextChanged(v => config.SetCoilAddressPrefix = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_PREFIX_TIMER_ADDRESS")
                         .ControlText(config.TimerAddressPrefix)
                         .TextChanged(v => config.TimerAddressPrefix = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.segmentNameConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(button.Text) { ControlWidth = 500 };

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_SEGMENT_NAME_ONE_EACH")
                         .ControlText(config.OneEachSegmentName)
                         .TextChanged(v => config.OneEachSegmentName = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_SEGMENT_NAME_ONE_EACH_EMPTY")
                         .ControlText(config.OneEachEmptyAlarmSegmentName)
                         .TextChanged(v => config.OneEachEmptyAlarmSegmentName = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_SEGMENT_NAME_GROUP_EACH")
                         .ControlText(config.GroupSegmentName)
                         .TextChanged(v => config.GroupSegmentName = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_SEGMENT_NAME_GROUP_EACH_EMPTY")
                         .ControlText(config.GroupEmptyAlarmSegmentName)
                         .TextChanged(v => config.GroupEmptyAlarmSegmentName = v);

                    SetupConfigForm(button, configForm);
                };
            }

            {
                var button = form.textListConfigButton;
                button.Click += (sender, args) =>
                {
                    var configForm = new ConfigForm(Localization.Get("ALARM_CONFIG_TEXT_LIST"));

                    var mainGroup = configForm.Init();
                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_TEXT_LIST_FULL")
                         .ControlText(config.AlarmTextInList)
                         .TextChanged(v => config.AlarmTextInList = v);

                    mainGroup.AddTextBox().LocalizedLabel("ALARM_CONFIG_TEXT_LIST_EMPTY")
                         .ControlText(config.EmptyAlarmTextInList)
                         .TextChanged(v => config.EmptyAlarmTextInList = v);

                    SetupConfigForm(button, configForm);
                };
            }
        }

        private void SetupConfigForm(Control button, ConfigForm configForm)
        {
            configForm.StartShowingAtControl(button);
            //configForm.Init();
            configForm.Show(form);
        }

        public void Translate()
        {
            form.partitionTypeLabel.Text = Localization.Get("ALARM_CONFIG_PARTITION_TYPE");
            form.groupingTypeLabel.Text = Localization.Get("ALARM_CONFIG_GROUPING_TYPE");

            form.fcConfigButton.Text = Localization.Get("ALARM_CONFIG_FC");
            form.alarmGenerationConfigButton.Text = Localization.Get("ALARM_CONFIG_GENERATION");
            form.emptyAlarmGenerationConfigButton.Text = Localization.Get("ALARM_CONFIG_GENERATION_EMPTY");
            form.fieldDefaultValueConfigButton.Text = Localization.Get("ALARM_CONFIG_DEFAULTS");
            form.fieldPrefixConfigButton.Text = Localization.Get("ALARM_CONFIG_PREFIX");
            form.segmentNameConfigButton.Text = Localization.Get("ALARM_CONFIG_SEGMENT_NAME");
            form.textListConfigButton.Text = Localization.Get("ALARM_CONFIG_TEXT_LIST");
        }
    }
}
