﻿using OfficeOpenXml;
using OfficeOpenXml.Style;
using SH_OBD_DLL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SH_OBD_Main {
    public class OBDTest {
        private readonly OBDIfEx _obdIfEx;
        private readonly DataTable _dtInfo;
        private readonly DataTable _dtECUInfo;
        private readonly DataTable _dtIUPR;
        private bool _compIgn;
        private bool _CN6;
        public event Action OBDTestStart;
        public event Action SetupColumnsDone;
        public event Action WriteDbStart;
        public event Action WriteDbDone;
        public event Action ReadDataFromDBDone;
        public event EventHandler<SetDataTableColumnsErrorEventArgs> SetDataTableColumnsError;

        public ModelLocal DbLocal { get; }
        public bool AdvancedMode { get; set; }
        public int AccessAdvancedMode { get; set; }
        public bool OBDResult { get; set; }
        public bool DTCResult { get; set; }
        public bool ReadinessResult { get; set; }
        public bool VINResult { get; set; }
        public bool CALIDCVNResult { get; set; }
        public bool SpaceResult { get; set; }
        public bool CALIDCheckResult { get; set; }
        public bool CVNCheckResult { get; set; }
        public bool VehicleTypeExist { get; set; }
        public string StrVIN_IN { get; set; }
        public string StrVIN_ECU { get; set; }
        public string StrType_IN { get; set; }
        public List<CheckResult> Checks { get; set; }


        public OBDTest(OBDIfEx obdIfex) {
            _obdIfEx = obdIfex;
            _dtInfo = new DataTable("Info");
            _dtECUInfo = new DataTable("ECUInfo");
            _dtIUPR = new DataTable("IUPR");
            _compIgn = false;
            _CN6 = false;
            AdvancedMode = false;
            AccessAdvancedMode = 0;
            OBDResult = false;
            DTCResult = true;
            ReadinessResult = true;
            VINResult = true;
            CALIDCVNResult = true;
            SpaceResult = true;
            CALIDCheckResult = true;
            CVNCheckResult = true;
            VehicleTypeExist = true;
            StrVIN_ECU = "";
            StrVIN_IN = "";
            StrType_IN = "";
            DbLocal = new ModelLocal(_obdIfEx.DBandMES, LibBase.DataBaseType.SQLite, _obdIfEx.Log);
            Checks = new List<CheckResult>();
        }

        public DataTable GetDataTable(DataTableType dtType) {
            switch (dtType) {
            case DataTableType.dtInfo:
                return _dtInfo;
            case DataTableType.dtECUInfo:
                return _dtECUInfo;
            case DataTableType.dtIUPR:
                return _dtIUPR;
            default:
                return null;
            }
        }

        private void SetDataTableColumns<T>(DataTable dt, Dictionary<string, bool[]> ECUSupports, bool bIUPR = false) {
            dt.Clear();
            dt.Columns.Clear();
            dt.Columns.Add(new DataColumn("NO", typeof(int)));
            dt.Columns.Add(new DataColumn("Item", typeof(string)));
            foreach (string key in ECUSupports.Keys) {
                if (bIUPR) {
                    if (ECUSupports[key][0xB - 1] || ECUSupports[key][0x8 - 1]) {
                        dt.Columns.Add(new DataColumn(key, typeof(T)));
                    }
                } else {
                    dt.Columns.Add(new DataColumn(key, typeof(T)));
                }
            }
        }

        private void SetDataRow(int lineNO, string strItem, DataTable dt, OBDParameter param) {
            Dictionary<string, bool[]> support = new Dictionary<string, bool[]>();
            if (param.Service == 1 || ((param.Parameter >> 8) & 0x00FF) == 0xF4) {
                support = _obdIfEx.OBDDll.Mode01Support;
            } else if (param.Service == 9 || ((param.Parameter >> 8) & 0x00FF) == 0xF8) {
                support = _obdIfEx.OBDDll.Mode09Support;
            }
            DataRow dr = dt.NewRow();
            dr[0] = lineNO;
            dr[1] = strItem;

            List<OBDParameterValue> valueList = _obdIfEx.OBDIf.GetValueList(param);
            if ((param.ValueTypes & (int)OBDParameter.EnumValueTypes.ListString) != 0) {
                int maxLine = 0;
                foreach (OBDParameterValue value in valueList) {
                    if (value.ErrorDetected) {
                        for (int i = 2; i < dt.Columns.Count; i++) {
                            dr[i] = "";
                        }
                        break;
                    }
                    if (value.ListStringValue != null) {
                        if (value.ListStringValue.Count > maxLine) {
                            maxLine = value.ListStringValue.Count;
                        }
                        for (int i = 2; i < dt.Columns.Count; i++) {
                            if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                                if (value.ListStringValue.Count == 0 || value.ListStringValue[0].Length == 0) {
                                    dr[i] = "";
                                } else {
                                    dr[i] = value.ListStringValue[0];
                                    for (int j = 1; j < value.ListStringValue.Count; j++) {
                                        dr[i] += "\n" + value.ListStringValue[j];
                                    }
                                }
                            }
                        }
                    }
                }
                if (param.Service == 1 || param.Service == 9 || param.Service == 0x22) {
                    for (int i = 2; i < dt.Columns.Count; i++) {
                        if (support.ContainsKey(dt.Columns[i].ColumnName) && !support[dt.Columns[i].ColumnName][(param.Parameter & 0x00FF) - 1]) {
                            dr[i] = "不适用";
                        }
                    }
                }
                dt.Rows.Add(dr);
            } else {
                foreach (OBDParameterValue value in valueList) {
                    if (value.ErrorDetected) {
                        for (int i = 2; i < dt.Columns.Count; i++) {
                            dr[i] = "";
                        }
                        break;
                    }
                    for (int i = 2; i < dt.Columns.Count; i++) {
                        if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                            if ((param.ValueTypes & (int)OBDParameter.EnumValueTypes.Bool) != 0) {
                                if (value.BoolValue) {
                                    dr[i] = "ON";
                                } else {
                                    dr[i] = "OFF";
                                }
                            } else if ((param.ValueTypes & (int)OBDParameter.EnumValueTypes.Double) != 0) {
                                dr[i] = value.DoubleValue.ToString();
                            } else if ((param.ValueTypes & (int)OBDParameter.EnumValueTypes.String) != 0) {
                                dr[i] = value.StringValue;
                            } else if ((param.ValueTypes & (int)OBDParameter.EnumValueTypes.ShortString) != 0) {
                                dr[i] = value.ShortStringValue;
                            }
                        }
                    }
                }
                if (param.Service == 1 || param.Service == 9 || param.Service == 0x22) {
                    for (int i = 2; i < dt.Columns.Count; i++) {
                        if (support.ContainsKey(dt.Columns[i].ColumnName) && !support[dt.Columns[i].ColumnName][(param.Parameter & 0x00FF) - 1]) {
                            dr[i] = "不适用";
                        }
                    }
                }
                dt.Rows.Add(dr);
            }
        }

        private void SetReadinessDataRow(int lineNO, string strItem, DataTable dt, List<OBDParameterValue> valueList, string sigName, ref int errorCount) {
            DataRow dr = dt.NewRow();
            dr[0] = lineNO;
            dr[1] = strItem;
            foreach (OBDParameterValue value in valueList) {
                if (value.ErrorDetected) {
                    for (int i = 2; i < dt.Columns.Count; i++) {
                        dr[i] = "";
                    }
                    break;
                }
                for (int i = 2; i < dt.Columns.Count; i++) {
                    if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                        if (sigName == "不适用") {
                            dr[i] = sigName;
                        } else {
                            foreach (string name in value.Message.Signals.Keys) {
                                if (name == sigName) {
                                    dr[i] = value.Message.Signals[name].DisplayString;
                                }
                            }
                        }
                    }
                }
            }
            for (int i = 2; i < dt.Columns.Count; i++) {
                if (_obdIfEx.OBDDll.Mode01Support.ContainsKey(dt.Columns[i].ColumnName) && !_obdIfEx.OBDDll.Mode01Support[dt.Columns[i].ColumnName][0]) {
                    dr[i] = "不适用";
                }
            }
            dt.Rows.Add(dr);
            for (int i = 2; i < dt.Columns.Count; i++) {
                if (dt.Rows[lineNO - 1][i].ToString() == "未完成") {
                    ++errorCount;
                }
            }
        }

        private void SetIUPRDataRow(int lineNO, string strItem, int padTotal, int padNum, DataTable dt, List<OBDParameterValue> valueList, string sigName, bool supported) {
            double num = 0;
            double den = 0;
            DataRow dr = dt.NewRow();
            dr[0] = lineNO;
            foreach (OBDParameterValue value in valueList) {
                for (int i = 2; i < dt.Columns.Count; i++) {
                    if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                        if (supported) {
                            if (dr[1].ToString().Length == 0) {
                                dr[1] = strItem + ": " + "监测完成次数".PadLeft(padTotal - padNum + 6);
                            }
                            foreach (string name in value.Message.Signals.Keys) {
                                if (name == sigName) {
                                    num = value.Message.Signals[name].Value;
                                    dr[i] = value.Message.Signals[name].DisplayString;
                                }
                            }
                        }
                    }
                }
            }
            if (dr[1].ToString().Length > 0) {
                dt.Rows.Add(dr);
            }

            dr = dt.NewRow();
            foreach (OBDParameterValue value in valueList) {
                for (int i = 2; i < dt.Columns.Count; i++) {
                    if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                        if (supported) {
                            if (dr[1].ToString().Length == 0) {
                                dr[1] = "符合监测条件次数".PadLeft(padTotal + 15);
                            }
                            foreach (string name in value.Message.Signals.Keys) {
                                if (name == sigName.Replace("COMP", "COND")) {
                                    den = value.Message.Signals[name].Value;
                                    dr[i] = value.Message.Signals[name].DisplayString;
                                }
                            }
                        }
                    }
                }
            }
            if (dr[1].ToString().Length > 0) {
                dt.Rows.Add(dr);
            }

            dr = dt.NewRow();
            foreach (OBDParameterValue value in valueList) {
                for (int i = 2; i < dt.Columns.Count; i++) {
                    if (dt.Columns[i].ColumnName == value.ECUResponseID) {
                        if (supported) {
                            if (dr[1].ToString().Length == 0) {
                                dr[1] = "IUPR率".PadLeft(padTotal + 12);
                            }
                            if (den == 0) {
                                dr[i] = "7.99527";
                            } else {
                                double r = Math.Round(num / den, 6);
                                if (r > 7.99527) {
                                    dr[i] = "7.99527";
                                } else {
                                    dr[i] = r.ToString();
                                }
                            }
                        }
                    }
                }
            }
            if (dr[1].ToString().Length > 0) {
                dt.Rows.Add(dr);
            }
        }

        private void SetDataTableInfo() {
            DataTable dt = _dtInfo;
            int NO = 0;
            OBDParameter param;
            int HByte = 0;
            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param = new OBDParameter {
                    OBDRequest = "22F401",
                    Service = 0x22,
                    Parameter = 0xF401,
                    SignalName = "MIL",
                    ValueTypes = (int)OBDParameter.EnumValueTypes.Bool
                };
                HByte = 0xF400;
            } else {
                param = new OBDParameter {
                    OBDRequest = "0101",
                    Service = 1,
                    Parameter = 1,
                    SignalName = "MIL",
                    ValueTypes = (int)OBDParameter.EnumValueTypes.Bool
                };
            }
            SetDataRow(++NO, "MIL状态", dt, param);                                          // 0

            param.Parameter = HByte + 0x21;
            param.SignalName = "MIL_DIST";
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.Double;
            SetDataRow(++NO, "MIL亮后行驶里程（km）", dt, param);                              // 1  

            param.Parameter = HByte + 0x1C;
            param.SignalName = "OBDSUP";
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.ShortString;
            SetDataRow(++NO, "OBD型式检验类型", dt, param);                                    // 2
            string OBD_SUP = dt.Rows[dt.Rows.Count - 1][2].ToString().Replace("不适用", "0").Split(',')[0];
            string[] CN6_OBD_SUP = _obdIfEx.OBDIf.DllSettings.CN6_OBD_SUP.Split(',');
            foreach (string item in CN6_OBD_SUP) {
                if (OBD_SUP == item) {
                    _CN6 = true;
                    break;
                }
            }

            param.Parameter = HByte + 0xA6;
            param.SignalName = "ODO";
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.Double;
            SetDataRow(++NO, "总累积里程ODO（km）", dt, param);                                // 3

            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param.OBDRequest = "194233081E";
            } else {
                param.OBDRequest = "03";
            }
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.ListString;
            SetDataRow(++NO, "存储DTC", dt, param);                                           // 4
            for (int i = 2; i < dt.Columns.Count; i++) {
                string DTC = dt.Rows[dt.Rows.Count - 1][i].ToString();
                if (_obdIfEx.OBDResultSetting.DTC03 && DTC != "--" && DTC != "不适用" && DTC.Length > 0) {
                    DTCResult = false;
                }
            }

            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param.OBDRequest = "194233041E";
            } else {
                param.OBDRequest = "07";
            }
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.ListString;
            SetDataRow(++NO, "未决DTC", dt, param);                                           // 5
            for (int i = 2; i < dt.Columns.Count; i++) {
                string DTC = dt.Rows[dt.Rows.Count - 1][i].ToString();
                if (_obdIfEx.OBDResultSetting.DTC07 && DTC != "--" && DTC != "不适用" && DTC.Length > 0) {
                    DTCResult = false;
                }
            }

            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param.OBDRequest = "195533";
            } else {
                param.OBDRequest = "0A";
            }
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.ListString;
            SetDataRow(++NO, "永久DTC", dt, param);                                           // 6
            for (int i = 2; i < dt.Columns.Count; i++) {
                string DTC = dt.Rows[dt.Rows.Count - 1][i].ToString();
                if (_obdIfEx.OBDResultSetting.DTC0A && DTC != "--" && DTC != "不适用" && DTC.Length > 0 && _CN6) {
                    DTCResult = false;
                }
            }

            int errorCount = 0;
            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param.OBDRequest = "22F401";
            } else {
                param.OBDRequest = "0101";
            }
            param.ValueTypes = (int)OBDParameter.EnumValueTypes.BitFlags;
            List<OBDParameterValue> valueList = _obdIfEx.OBDIf.GetValueList(param);
            SetReadinessDataRow(++NO, "失火监测", dt, valueList, "MIS_RDY", ref errorCount);       // 7
            SetReadinessDataRow(++NO, "燃油系统监测", dt, valueList, "FUEL_RDY", ref errorCount);  // 8
            SetReadinessDataRow(++NO, "综合组件监测", dt, valueList, "CCM_RDY", ref errorCount);   // 9

            foreach (OBDParameterValue value in valueList) {
                if (_obdIfEx.OBDDll.Mode01Support.ContainsKey(value.ECUResponseID) && _obdIfEx.OBDDll.Mode01Support[value.ECUResponseID][(param.Parameter & 0x00FF) - 1]) {
                    _compIgn = value.GetBitFlag(12);
                    break;
                }
            }
            if (_compIgn) {
                // 压缩点火
                SetReadinessDataRow(++NO, "NMHC催化剂监测", dt, valueList, "HCCATRDY", ref errorCount);             // 10
                SetReadinessDataRow(++NO, "NOx/SCR后处理监测", dt, valueList, "NCAT_RDY", ref errorCount);          // 11
                SetReadinessDataRow(++NO, "增压系统监测", dt, valueList, "BP_RDY", ref errorCount);                 // 12
                SetReadinessDataRow(++NO, "排气传感器监测", dt, valueList, "EGS_RDY", ref errorCount);              // 13
                SetReadinessDataRow(++NO, "PM过滤器监测", dt, valueList, "PM_RDY", ref errorCount);                 // 14
                SetReadinessDataRow(++NO, "EGR/VVT系统监测", dt, valueList, "EGR_RDY_compression", ref errorCount); // 15
            } else {
                // 火花点火
                SetReadinessDataRow(++NO, "催化剂监测", dt, valueList, "CAT_RDY", ref errorCount);              // 10
                SetReadinessDataRow(++NO, "加热催化剂监测", dt, valueList, "HCAT_RDY", ref errorCount);          // 11
                SetReadinessDataRow(++NO, "燃油蒸发系统监测", dt, valueList, "EVAP_RDY", ref errorCount);        // 12
                SetReadinessDataRow(++NO, "二次空气系统监测", dt, valueList, "AIR_RDY", ref errorCount);         // 13
                SetReadinessDataRow(++NO, "空调系统制冷剂监测", dt, valueList, "不适用", ref errorCount);        // 14
                SetReadinessDataRow(++NO, "氧气传感器监测", dt, valueList, "O2S_RDY", ref errorCount);           // 15
                SetReadinessDataRow(++NO, "加热氧气传感器监测", dt, valueList, "HTR_RDY", ref errorCount);       // 16
                SetReadinessDataRow(++NO, "EGR/VVT系统监测", dt, valueList, "EGR_RDY_spark", ref errorCount);   // 17
            }
            if (_obdIfEx.OBDResultSetting.Readiness && errorCount > 2) {
                ReadinessResult = false;
            }
        }

        private void SetDataTableECUInfo() {
            DataTable dt = _dtECUInfo;
            int NO = 0;
            OBDParameter param;
            int HByte = 0;
            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param = new OBDParameter {
                    OBDRequest = "22F802",
                    Service = 0x22,
                    Parameter = 0xF802,
                    ValueTypes = (int)OBDParameter.EnumValueTypes.ListString
                };
                HByte = 0xF800;
            } else {
                param = new OBDParameter {
                    OBDRequest = "0902",
                    Service = 9,
                    Parameter = 2,
                    SignalName = "VIN",
                    ValueTypes = (int)OBDParameter.EnumValueTypes.ListString
                };
            }
            SetDataRow(++NO, "VIN", dt, param);     // 0
            string strVIN = "";
            for (int i = 2; i < dt.Columns.Count; i++) {
                strVIN = dt.Rows[0][i].ToString();
                if (strVIN.Length > 0 || strVIN != "不适用" || strVIN != "--") {
                    break;
                }
            }
            StrVIN_ECU = strVIN;
            if (_obdIfEx.OBDResultSetting.VINError && StrVIN_IN != null && StrVIN_ECU != StrVIN_IN && StrVIN_IN.Length > 0) {
                _obdIfEx.Log.TraceWarning("Scan tool VIN[" + StrVIN_IN + "] and ECU VIN[" + StrVIN_ECU + "] are not consistent");
                VINResult = false;
            }
            param.Parameter = HByte + 0x0A;
            param.SignalName = "ECU_NAME";
            SetDataRow(++NO, "ECU名称", dt, param); // 1
            param.Parameter = HByte + 4;
            param.SignalName = "CAL_ID";
            SetDataRow(++NO, "CAL_ID", dt, param);  // 2
            param.Parameter = HByte + 6;
            param.SignalName = "CVN";
            SetDataRow(++NO, "CVN", dt, param);     // 3

            // 根据配置文件，判断CAL_ID和CVN两个值的合法性
            if (_CN6) {
                for (int i = 2; i < dt.Columns.Count; i++) {
                    string[] CALIDArray = dt.Rows[2][i].ToString().Split('\n');
                    string[] CVNArray = dt.Rows[3][i].ToString().Split('\n');
                    int length = Math.Max(CALIDArray.Length, CVNArray.Length);
                    for (int j = 0; j < length; j++) {
                        string CALID = CALIDArray.Length > j ? CALIDArray[j] : "";
                        string CVN = CVNArray.Length > j ? CVNArray[j] : "";
                        if (!_obdIfEx.OBDResultSetting.CALIDCVNEmpty) {
                            if (CALID.Length * CVN.Length == 0) {
                                if (CALID.Length + CVN.Length == 0) {
                                    if (j == 0) {
                                        CALIDCVNResult = false;
                                    }
                                } else {
                                    CALIDCVNResult = false;
                                }
                            }
                        }
                        CheckResult check = new CheckResult {
                            ECUID = dt.Columns[i].ColumnName,
                            Index = j
                        };
                        Checks.Add(check);
                        CheckCALIDCVN(StrType_IN, dt.Columns[i].ColumnName, CALID, CVN, check);
                    }
                }
            }

        }

        private void SetDataTableIUPR() {
            DataTable dt = _dtIUPR;
            int NO = 0;
            OBDParameter param;
            List<OBDParameterValue> valueList;
            int HByte = 0;
            if (_obdIfEx.OBDIf.STDType == StandardType.ISO_27145) {
                param = new OBDParameter {
                    OBDRequest = "22F80B",
                    Service = 0x22,
                    Parameter = 0xF80B,
                    ValueTypes = (int)OBDParameter.EnumValueTypes.ListString
                };
                HByte = 0xF800;
            } else {
                param = new OBDParameter {
                    OBDRequest = "090B",
                    Service = 9,
                    Parameter = 0x0B,
                    ValueTypes = (int)OBDParameter.EnumValueTypes.ListString
                };
            }
            for (int i = 2; i < dt.Columns.Count; i++) {
                // 压缩点火
                bool supported = _obdIfEx.OBDDll.Mode09Support.ContainsKey(dt.Columns[i].ColumnName);
                supported = supported && _obdIfEx.OBDDll.Mode09Support[dt.Columns[i].ColumnName][param.Parameter - HByte - 1];
                if (supported) {
                    valueList = _obdIfEx.OBDIf.GetValueList(param);
                    SetIUPRDataRow(++NO, "NMHC催化器", 18, 16, dt, valueList, "HCCATCOMP", supported);
                    SetIUPRDataRow(++NO, "NOx催化器", 18, 12, dt, valueList, "NCATCOMP", supported);
                    SetIUPRDataRow(++NO, "NOx吸附器", 18, 12, dt, valueList, "NADSCOMP", supported);
                    SetIUPRDataRow(++NO, "PM捕集器", 18, 10, dt, valueList, "PMCOMP", supported);
                    SetIUPRDataRow(++NO, "废气传感器", 18, 12, dt, valueList, "EGSCOMP", supported);
                    SetIUPRDataRow(++NO, "EGR和VVT", 18, 12, dt, valueList, "EGRCOMP", supported);
                    SetIUPRDataRow(++NO, "增压压力", 18, 8, dt, valueList, "BPCOMP", supported);
                }
                // 火花点火
                NO = 0;
                param.Parameter = HByte + 8;
                supported = _obdIfEx.OBDDll.Mode09Support.ContainsKey(dt.Columns[i].ColumnName);
                supported = supported && _obdIfEx.OBDDll.Mode09Support[dt.Columns[i].ColumnName][param.Parameter - HByte - 1];
                if (supported) {
                    valueList = _obdIfEx.OBDIf.GetValueList(param);
                    SetIUPRDataRow(++NO, "催化器 组1", 18, 11, dt, valueList, "CATCOMP1", supported);
                    SetIUPRDataRow(++NO, "催化器 组2", 18, 11, dt, valueList, "CATCOMP2", supported);
                    SetIUPRDataRow(++NO, "前氧传感器 组1", 18, 18, dt, valueList, "O2SCOMP1", supported);
                    SetIUPRDataRow(++NO, "前氧传感器 组2", 18, 18, dt, valueList, "O2SCOMP2", supported);
                    SetIUPRDataRow(++NO, "后氧传感器 组1", 18, 18, dt, valueList, "SO2SCOMP1", supported);
                    SetIUPRDataRow(++NO, "后氧传感器 组2", 18, 18, dt, valueList, "SO2SCOMP2", supported);
                    SetIUPRDataRow(++NO, "EVAP", 18, 4, dt, valueList, "EVAPCOMP", supported);
                    SetIUPRDataRow(++NO, "EGR和VVT", 18, 12, dt, valueList, "EGRCOMP", supported);
                    SetIUPRDataRow(++NO, "GPF 组1", 18, 8, dt, valueList, "PFCOMP1", supported);
                    SetIUPRDataRow(++NO, "GPF 组2", 18, 8, dt, valueList, "PFCOMP2", supported);
                    SetIUPRDataRow(++NO, "二次空气喷射系统", 18, 18, dt, valueList, "AIRCOMP", supported);
                }
            }
        }

        /// <summary>
        /// 返回值代表检测数据上传后是否返回成功信息
        /// </summary>
        /// <param name="errorMsg">错误信息</param>
        /// <returns>是否返回成功信息</returns>
        public bool StartOBDTest(out string errorMsg) {
            _obdIfEx.Log.TraceInfo(string.Format(">>>>> Enter StartOBDTest function. Ver(Main / Dll): {0} / {1} <<<<<", MainFileVersion.AssemblyVersion, DllVersion<SH_OBD_Dll>.AssemblyVersion));
            errorMsg = "";
            _dtInfo.Clear();
            _dtInfo.Dispose();
            _dtECUInfo.Clear();
            _dtECUInfo.Dispose();
            _dtIUPR.Clear();
            _dtIUPR.Dispose();
            _compIgn = false;
            _CN6 = false;
            OBDResult = false;
            DTCResult = true;
            ReadinessResult = true;
            VINResult = true;
            CALIDCVNResult = true;
            SpaceResult = true;
            CALIDCheckResult = true;
            CVNCheckResult = true;
            VehicleTypeExist = true;
            Checks = new List<CheckResult>();

            OBDTestStart?.Invoke();

            if (!_obdIfEx.OBDDll.SetSupportStatus(out errorMsg)) {
                SetupColumnsDone?.Invoke();
                throw new Exception(errorMsg);
            }

            SetDataTableColumns<string>(_dtInfo, _obdIfEx.OBDDll.Mode01Support);
            SetDataTableColumns<string>(_dtECUInfo, _obdIfEx.OBDDll.Mode09Support);
            SetDataTableColumns<string>(_dtIUPR, _obdIfEx.OBDDll.Mode09Support, true);
            SetupColumnsDone?.Invoke();
            SetDataTableInfo();
            SetDataTableECUInfo();
            SetDataTableIUPR();

            OBDResult = DTCResult && ReadinessResult && VINResult && CALIDCVNResult && SpaceResult && CALIDCheckResult && CVNCheckResult && VehicleTypeExist;
            string strLog = "OBD Test Result: " + OBDResult.ToString() + " [";
            strLog += "DTCResult: " + DTCResult.ToString();
            strLog += ", ReadinessResult: " + ReadinessResult.ToString();
            strLog += ", SpaceResult: " + SpaceResult.ToString();
            strLog += ", VINResult: " + VINResult.ToString();
            strLog += ", CALIDCVNResult: " + CALIDCVNResult.ToString();
            strLog += ", VehicleTypeExist: " + VehicleTypeExist.ToString();
            strLog += ", CALIDCheckResult: " + CALIDCheckResult.ToString();
            strLog += ", CVNCheckResult: " + CVNCheckResult.ToString() + "]";
            _obdIfEx.Log.TraceInfo(strLog);

            WriteDbStart?.Invoke();
            string strVIN = "";
            for (int i = 2; i < _dtECUInfo.Columns.Count; i++) {
                strVIN = _dtECUInfo.Rows[0][i].ToString();
                if (strVIN.Length > 0 || strVIN != "不适用" || strVIN != "--") {
                    break;
                }
            }
            string strOBDResult = OBDResult ? "1" : "0";

            DataTable dt = new DataTable("OBDData");
            DbLocal.GetEmptyTable(dt);
            dt.Columns.Remove("ID");
            dt.Columns.Remove("WriteTime");
            try {
                SetDataTableResult(StrVIN_ECU, strOBDResult, dt);
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("Result DataTable Error: " + ex.Message);
                dt.Dispose();
                WriteDbDone?.Invoke();
                throw new ApplicationException("生成 Result DataTable 出错");
            }

            DataTable dtIUPR = new DataTable("OBDIUPR");
            DbLocal.GetEmptyTable(dtIUPR);
            dtIUPR.Columns.Remove("ID");
            dtIUPR.Columns.Remove("WriteTime");
            SetDataTableResultIUPR(StrVIN_ECU, dtIUPR);

            DbLocal.ModifyRecords(dt);
            DbLocal.ModifyRecords(dtIUPR);
            WriteDbDone?.Invoke();

            try {
                ExportResultFile(dt);
                ExportXmlResult(dt);
            } catch (Exception ex) {
                _obdIfEx.Log.TraceError("Exporting OBD test result file failed: " + ex.Message);
                dt.Dispose();
                throw new Exception("生成OBD检测结果文件出错");
            }

            dt.Dispose();
            return OBDResult;
        }

        private void SetDataTableResult(string strVIN, string strOBDResult, DataTable dtOut) {
            string strDTCTemp;
            for (int i = 2; i < _dtECUInfo.Columns.Count; i++) {
                DataRow dr = dtOut.NewRow();
                dr["VIN"] = strVIN;
                dr["ECU_ID"] = _dtECUInfo.Columns[i].ColumnName;
                for (int j = 2; j < _dtInfo.Columns.Count; j++) {
                    if (_dtInfo.Columns[j].ColumnName == _dtECUInfo.Columns[i].ColumnName) {
                        dr["MIL"] = _dtInfo.Rows[0][j];
                        dr["MIL_DIST"] = _dtInfo.Rows[1][j];
                        dr["OBD_SUP"] = _dtInfo.Rows[2][j];
                        dr["ODO"] = _dtInfo.Rows[3][j];
                        // DTC03，若大于数据库字段容量则截断
                        strDTCTemp = _dtInfo.Rows[4][j].ToString().Replace("\n", ",");
                        if (strDTCTemp.Length > 100) {
                            dr["DTC03"] = strDTCTemp.Substring(0, 97) + "...";
                        } else {
                            dr["DTC03"] = strDTCTemp;
                        }
                        // DTC07，若大于数据库字段容量则截断
                        strDTCTemp = _dtInfo.Rows[5][j].ToString().Replace("\n", ",");
                        if (strDTCTemp.Length > 100) {
                            dr["DTC07"] = strDTCTemp.Substring(0, 97) + "...";
                        } else {
                            dr["DTC07"] = strDTCTemp;
                        }
                        // DTC0A，若大于数据库字段容量则截断
                        strDTCTemp = _dtInfo.Rows[6][j].ToString().Replace("\n", ",");
                        if (strDTCTemp.Length > 100) {
                            dr["DTC0A"] = strDTCTemp.Substring(0, 97) + "...";
                        } else {
                            dr["DTC0A"] = strDTCTemp;
                        }
                        dr["MIS_RDY"] = _dtInfo.Rows[7][j];
                        dr["FUEL_RDY"] = _dtInfo.Rows[8][j];
                        dr["CCM_RDY"] = _dtInfo.Rows[9][j];
                        if (_compIgn) {
                            dr["CAT_RDY"] = "不适用";
                            dr["HCAT_RDY"] = "不适用";
                            dr["EVAP_RDY"] = "不适用";
                            dr["AIR_RDY"] = "不适用";
                            dr["ACRF_RDY"] = "不适用";
                            dr["O2S_RDY"] = "不适用";
                            dr["HTR_RDY"] = "不适用";
                            dr["EGR_RDY"] = _dtInfo.Rows[15][j];
                            dr["HCCAT_RDY"] = _dtInfo.Rows[10][j];
                            dr["NCAT_RDY"] = _dtInfo.Rows[11][j];
                            dr["BP_RDY"] = _dtInfo.Rows[12][j];
                            dr["EGS_RDY"] = _dtInfo.Rows[13][j];
                            dr["PM_RDY"] = _dtInfo.Rows[14][j];
                        } else {
                            dr["CAT_RDY"] = _dtInfo.Rows[10][j];
                            dr["HCAT_RDY"] = _dtInfo.Rows[11][j];
                            dr["EVAP_RDY"] = _dtInfo.Rows[12][j];
                            dr["AIR_RDY"] = _dtInfo.Rows[13][j];
                            dr["ACRF_RDY"] = _dtInfo.Rows[14][j];
                            dr["O2S_RDY"] = _dtInfo.Rows[15][j];
                            dr["HTR_RDY"] = _dtInfo.Rows[16][j];
                            dr["EGR_RDY"] = _dtInfo.Rows[17][j];
                            dr["HCCAT_RDY"] = "不适用";
                            dr["NCAT_RDY"] = "不适用";
                            dr["BP_RDY"] = "不适用";
                            dr["EGS_RDY"] = "不适用";
                            dr["PM_RDY"] = "不适用";
                        }
                        break;
                    }
                }
                dr["ECU_NAME"] = _dtECUInfo.Rows[1][i].ToString().Replace("\n", ",");
                dr["CAL_ID"] = _dtECUInfo.Rows[2][i].ToString().Replace("\n", ",");
                dr["CVN"] = _dtECUInfo.Rows[3][i].ToString().Replace("\n", ",");
                dr["Result"] = strOBDResult;
                dr["Upload"] = "0";
                dtOut.Rows.Add(dr);
            }
        }

        private void SetDataTableResultIUPR(string strVIN, DataTable dtOut) {
            for (int i = 2; i < _dtIUPR.Columns.Count; i++) {
                DataRow dr = dtOut.NewRow();
                dr["VIN"] = strVIN;
                dr["ECU_ID"] = _dtIUPR.Columns[i].ColumnName;
                if (_obdIfEx.OBDDll.Mode09Support.ContainsKey(_dtIUPR.Columns[i].ColumnName) && _obdIfEx.OBDDll.Mode09Support[_dtIUPR.Columns[i].ColumnName][0x08 - 1] && _dtIUPR.Rows.Count > 0) {
                    dr["CATCOMP1"] = _dtIUPR.Rows[0][i];
                    dr["CATCOND1"] = _dtIUPR.Rows[1][i];
                    dr["CATCOMP2"] = _dtIUPR.Rows[3][i];
                    dr["CATCOND2"] = _dtIUPR.Rows[4][i];
                    dr["O2SCOMP1"] = _dtIUPR.Rows[6][i];
                    dr["O2SCOND1"] = _dtIUPR.Rows[7][i];
                    dr["O2SCOMP2"] = _dtIUPR.Rows[9][i];
                    dr["O2SCOND2"] = _dtIUPR.Rows[10][i];
                    dr["SO2SCOMP1"] = _dtIUPR.Rows[12][i];
                    dr["SO2SCOND1"] = _dtIUPR.Rows[13][i];
                    dr["SO2SCOMP2"] = _dtIUPR.Rows[15][i];
                    dr["SO2SCOND2"] = _dtIUPR.Rows[16][i];
                    dr["EVAPCOMP"] = _dtIUPR.Rows[18][i];
                    dr["EVAPCOND"] = _dtIUPR.Rows[19][i];
                    dr["EGRCOMP_08"] = _dtIUPR.Rows[21][i];
                    dr["EGRCOND_08"] = _dtIUPR.Rows[22][i];
                    dr["PFCOMP1"] = _dtIUPR.Rows[24][i];
                    dr["PFCOND1"] = _dtIUPR.Rows[25][i];
                    dr["PFCOMP2"] = _dtIUPR.Rows[27][i];
                    dr["PFCOND2"] = _dtIUPR.Rows[28][i];
                    dr["AIRCOMP"] = _dtIUPR.Rows[30][i];
                    dr["AIRCOND"] = _dtIUPR.Rows[31][i];
                } else {
                    dr["CATCOMP1"] = "-1";
                    dr["CATCOND1"] = "-1";
                    dr["CATCOMP2"] = "-1";
                    dr["CATCOND2"] = "-1";
                    dr["O2SCOMP1"] = "-1";
                    dr["O2SCOND1"] = "-1";
                    dr["O2SCOMP2"] = "-1";
                    dr["O2SCOND2"] = "-1";
                    dr["SO2SCOMP1"] = "-1";
                    dr["SO2SCOND1"] = "-1";
                    dr["SO2SCOMP2"] = "-1";
                    dr["SO2SCOND2"] = "-1";
                    dr["EVAPCOMP"] = "-1";
                    dr["EVAPCOND"] = "-1";
                    dr["EGRCOMP_08"] = "-1";
                    dr["EGRCOND_08"] = "-1";
                    dr["PFCOMP1"] = "-1";
                    dr["PFCOND1"] = "-1";
                    dr["PFCOMP2"] = "-1";
                    dr["PFCOND2"] = "-1";
                    dr["AIRCOMP"] = "-1";
                    dr["AIRCOND"] = "-1";
                }
                if (_obdIfEx.OBDDll.Mode09Support.ContainsKey(_dtIUPR.Columns[i].ColumnName) && _obdIfEx.OBDDll.Mode09Support[_dtIUPR.Columns[i].ColumnName][0x0B - 1] && _dtIUPR.Rows.Count > 0) {
                    dr["HCCATCOMP"] = _dtIUPR.Rows[0][i];
                    dr["HCCATCOND"] = _dtIUPR.Rows[1][i];
                    dr["NCATCOMP"] = _dtIUPR.Rows[3][i];
                    dr["NCATCOND"] = _dtIUPR.Rows[4][i];
                    dr["NADSCOMP"] = _dtIUPR.Rows[6][i];
                    dr["NADSCOND"] = _dtIUPR.Rows[7][i];
                    dr["PMCOMP"] = _dtIUPR.Rows[9][i];
                    dr["PMCOND"] = _dtIUPR.Rows[10][i];
                    dr["EGSCOMP"] = _dtIUPR.Rows[12][i];
                    dr["EGSCOND"] = _dtIUPR.Rows[13][i];
                    dr["EGRCOMP_0B"] = _dtIUPR.Rows[15][i];
                    dr["EGRCOND_0B"] = _dtIUPR.Rows[16][i];
                    dr["BPCOMP"] = _dtIUPR.Rows[18][i];
                    dr["BPCOND"] = _dtIUPR.Rows[19][i];
                } else {
                    dr["HCCATCOMP"] = "-1";
                    dr["HCCATCOND"] = "-1";
                    dr["NCATCOMP"] = "-1";
                    dr["NCATCOND"] = "-1";
                    dr["NADSCOMP"] = "-1";
                    dr["NADSCOND"] = "-1";
                    dr["PMCOMP"] = "-1";
                    dr["PMCOND"] = "-1";
                    dr["EGSCOMP"] = "-1";
                    dr["EGSCOND"] = "-1";
                    dr["EGRCOMP_0B"] = "-1";
                    dr["EGRCOND_0B"] = "-1";
                    dr["BPCOMP"] = "-1";
                    dr["BPCOND"] = "-1";
                }
                dtOut.Rows.Add(dr);
            }
        }

        private string GetModuleID(string ECUAcronym, string ECUID) {
            string moduleID = ECUAcronym;
            if (_obdIfEx.OBDResultSetting.UseECUAcronym) {
                if (moduleID.Length == 0 || moduleID == "不适用") {
                    moduleID = ECUID;
                }
            } else {
                moduleID = ECUID;
            }
            return moduleID;
        }

        private bool SetDataTableColumnsFromDB(DataTable dtDisplay, DataTable dtIn) {
            dtDisplay.Clear();
            dtDisplay.Columns.Clear();
            if (dtIn.Rows.Count > 0) {
                dtDisplay.Columns.Add(new DataColumn("NO", typeof(int)));
                dtDisplay.Columns.Add(new DataColumn("Item", typeof(string)));
                for (int i = 0; i < dtIn.Rows.Count; i++) {
                    dtDisplay.Columns.Add(new DataColumn(dtIn.Rows[i]["ECU_ID"].ToString(), typeof(string)));
                }
                return true;
            } else {
                SetDataTableColumnsErrorEventArgs args = new SetDataTableColumnsErrorEventArgs {
                    ErrorMsg = "从数据库中未获取到数据"
                };
                SetDataTableColumnsError?.Invoke(this, args);
                return false;
            }
        }

        private void SetDataRowInfoFromDB(int lineNO, string strItem, DataTable dtIn, bool bCompIgn = false) {
            DataRow dr = _dtInfo.NewRow();
            dr[0] = lineNO;
            dr[1] = strItem;
            for (int i = 0; i < dtIn.Rows.Count; i++) {
                if (lineNO > 10 && bCompIgn) {
                    dr[i + 2] = dtIn.Rows[i][lineNO + (lineNO == 16 ? 3 : 9)];
                } else {
                    if (strItem.Contains("DTC")) {
                        dr[i + 2] = dtIn.Rows[i][lineNO + 1].ToString().Replace(",", "\n");
                    } else {
                        dr[i + 2] = dtIn.Rows[i][lineNO + 1];
                    }
                }
            }
            _dtInfo.Rows.Add(dr);
        }

        private void SetDataRowECUInfoFromDB(int lineNO, string strItem, DataTable dtIn) {
            DataRow dr = _dtECUInfo.NewRow();
            dr[0] = lineNO;
            dr[1] = strItem;
            for (int i = 0; i < dtIn.Rows.Count; i++) {
                if (lineNO == 1) {
                    dr[i + 2] = dtIn.Rows[i][lineNO - 1];
                } else {
                    dr[i + 2] = dtIn.Rows[i][lineNO + 23].ToString().Replace(",", "\n");
                }
            }
            _dtECUInfo.Rows.Add(dr);
        }

        private void SetDataRowIUPRFromDB(int lineNO, string strItem, int padTotal, int padNum, DataTable dtIn, bool bCompIgn) {
            double[] nums = new double[dtIn.Rows.Count];
            double[] dens = new double[dtIn.Rows.Count];
            int colIndex;
            if (bCompIgn) {
                colIndex = (lineNO + 11) * 2;
            } else {
                colIndex = lineNO * 2;
            }
            DataRow dr = _dtIUPR.NewRow();
            dr[0] = lineNO;
            if (dr[1].ToString().Length == 0) {
                dr[1] = strItem + ": " + "监测完成次数".PadLeft(padTotal - padNum + 6);
            }
            for (int i = 0; i < dtIn.Rows.Count; i++) {
                dr[i + 2] = dtIn.Rows[i][colIndex].ToString();
                int.TryParse(dtIn.Rows[i][colIndex].ToString(), out int temp);
                nums[i] = temp;
            }
            _dtIUPR.Rows.Add(dr);

            dr = _dtIUPR.NewRow();
            if (dr[1].ToString().Length == 0) {
                dr[1] = "符合监测条件次数".PadLeft(padTotal + 15);
            }
            for (int i = 0; i < dtIn.Rows.Count; i++) {
                dr[i + 2] = dtIn.Rows[i][colIndex + 1].ToString();
                int.TryParse(dtIn.Rows[i][colIndex + 1].ToString(), out int temp);
                dens[i] = temp;
            }
            _dtIUPR.Rows.Add(dr);

            dr = _dtIUPR.NewRow();
            if (dr[1].ToString().Length == 0) {
                dr[1] = "IUPR率".PadLeft(padTotal + 12);
            }
            for (int i = 0; i < dtIn.Rows.Count; i++) {
                if (dens[i] == 0) {
                    dr[i + 2] = "7.99527";
                } else {
                    double r = Math.Round(nums[i] / dens[i], 6);
                    if (r > 7.99527) {
                        dr[i + 2] = "7.99527";
                    } else {
                        dr[i + 2] = r.ToString();
                    }
                }
            }
            _dtIUPR.Rows.Add(dr);
        }

        private void SetDataTableInfoFromDB(DataTable dtIn) {
            if (_dtInfo.Columns.Count <= 0) {
                return;
            }
            int NO = 0;
            SetDataRowInfoFromDB(++NO, "MIL状态", dtIn);               // 1,2
            SetDataRowInfoFromDB(++NO, "MIL亮后行驶里程（km）", dtIn);  // 2,3
            SetDataRowInfoFromDB(++NO, "OBD型式检验类型", dtIn);        // 3,4
            SetDataRowInfoFromDB(++NO, "总累积里程ODO（km）", dtIn);    // 4,5
            SetDataRowInfoFromDB(++NO, "存储DTC", dtIn);               // 5,6
            SetDataRowInfoFromDB(++NO, "未决DTC", dtIn);               // 6,7
            SetDataRowInfoFromDB(++NO, "永久DTC", dtIn);               // 7,8
            SetDataRowInfoFromDB(++NO, "失火监测", dtIn);              // 8,9
            SetDataRowInfoFromDB(++NO, "燃油系统监测", dtIn);          // 9,10
            SetDataRowInfoFromDB(++NO, "综合组件监测", dtIn);          // 10,11
            bool bCompIgn = GetCompIgn(dtIn);
            if (bCompIgn) {
                // 压缩点火
                SetDataRowInfoFromDB(++NO, "NMHC催化剂监测", dtIn, bCompIgn);     // 11,20
                SetDataRowInfoFromDB(++NO, "NOx/SCR后处理监测", dtIn, bCompIgn);  // 12,21
                SetDataRowInfoFromDB(++NO, "增压系统监测", dtIn, bCompIgn);       // 13,22
                SetDataRowInfoFromDB(++NO, "排气传感器监测", dtIn, bCompIgn);     // 14,23
                SetDataRowInfoFromDB(++NO, "PM过滤器监测", dtIn, bCompIgn);       // 15,24
            } else {
                // 火花点火
                SetDataRowInfoFromDB(++NO, "催化剂监测", dtIn, bCompIgn);         // 11,12
                SetDataRowInfoFromDB(++NO, "加热催化剂监测", dtIn, bCompIgn);     // 12,13
                SetDataRowInfoFromDB(++NO, "燃油蒸发系统监测", dtIn, bCompIgn);   // 13,14
                SetDataRowInfoFromDB(++NO, "二次空气系统监测", dtIn, bCompIgn);   // 14,15
                SetDataRowInfoFromDB(++NO, "空调系统制冷剂监测", dtIn, bCompIgn); // 15,16
                SetDataRowInfoFromDB(++NO, "氧气传感器监测", dtIn, bCompIgn);     // 16,17
                SetDataRowInfoFromDB(++NO, "加热氧气传感器监测", dtIn, bCompIgn); // 17,18
            }
            SetDataRowInfoFromDB(++NO, "EGR/VVT系统监测", dtIn, bCompIgn);       // 16,19 / 18,19
        }

        private void SetDataTableECUInfoFromDB(DataTable dtIn) {
            if (_dtInfo.Columns.Count <= 0) {
                return;
            }
            int NO = 0;
            SetDataRowECUInfoFromDB(++NO, "VIN", dtIn);     // 1,0
            SetDataRowECUInfoFromDB(++NO, "ECU名称", dtIn); // 2,25
            SetDataRowECUInfoFromDB(++NO, "CAL_ID", dtIn);  // 3,26
            SetDataRowECUInfoFromDB(++NO, "CVN", dtIn);     // 4,27
        }

        private void SetDataTableIUPRFromDB(DataTable dtIn) {
            if (_dtIUPR.Columns.Count <= 0) {
                return;
            }
            int NO = 0;
            bool bCompIgnIUPR = GetCompIgnIUPR(dtIn);
            if (bCompIgnIUPR) {
                // 压缩点火
                SetDataRowIUPRFromDB(++NO, "NMHC催化器", 18, 16, dtIn, bCompIgnIUPR);  // 1,24
                SetDataRowIUPRFromDB(++NO, "NOx催化器", 18, 12, dtIn, bCompIgnIUPR);   // 2,26
                SetDataRowIUPRFromDB(++NO, "NOx吸附器", 18, 12, dtIn, bCompIgnIUPR);   // 3,28
                SetDataRowIUPRFromDB(++NO, "PM捕集器", 18, 10, dtIn, bCompIgnIUPR);    // 4,30
                SetDataRowIUPRFromDB(++NO, "废气传感器", 18, 12, dtIn, bCompIgnIUPR);  // 5,32
                SetDataRowIUPRFromDB(++NO, "EGR和VVT", 18, 12, dtIn, bCompIgnIUPR);   // 6,34
                SetDataRowIUPRFromDB(++NO, "增压压力", 18, 8, dtIn, bCompIgnIUPR);     // 7,36
            } else {
                // 火花点火
                NO = 0;
                SetDataRowIUPRFromDB(++NO, "催化器 组1", 18, 11, dtIn, bCompIgnIUPR);       // 1,2
                SetDataRowIUPRFromDB(++NO, "催化器 组2", 18, 11, dtIn, bCompIgnIUPR);       // 2,4
                SetDataRowIUPRFromDB(++NO, "前氧传感器 组1", 18, 18, dtIn, bCompIgnIUPR);   // 3,6
                SetDataRowIUPRFromDB(++NO, "前氧传感器 组2", 18, 18, dtIn, bCompIgnIUPR);   // 4,8
                SetDataRowIUPRFromDB(++NO, "后氧传感器 组1", 18, 18, dtIn, bCompIgnIUPR);   // 5,10
                SetDataRowIUPRFromDB(++NO, "后氧传感器 组2", 18, 18, dtIn, bCompIgnIUPR);   // 6,12
                SetDataRowIUPRFromDB(++NO, "EVAP", 18, 4, dtIn, bCompIgnIUPR);             // 7,14
                SetDataRowIUPRFromDB(++NO, "EGR和VVT", 18, 12, dtIn, bCompIgnIUPR);        // 8,16
                SetDataRowIUPRFromDB(++NO, "GPF 组1", 18, 8, dtIn, bCompIgnIUPR);          // 9,18
                SetDataRowIUPRFromDB(++NO, "GPF 组2", 18, 8, dtIn, bCompIgnIUPR);          // 10,20
                SetDataRowIUPRFromDB(++NO, "二次空气喷射系统", 18, 18, dtIn, bCompIgnIUPR); // 11,22
            }
        }

        public void ShowDataFromDB(string strVIN) {
            bool bRet = true;
            DataTable dt = new DataTable("OBDData");
            DbLocal.GetEmptyTable(dt);
            dt.Columns.Remove("ID");
            dt.Columns.Remove("WriteTime");
            Dictionary<string, string> whereDic = new Dictionary<string, string> { { "VIN", strVIN } };
            DbLocal.GetRecords(dt, whereDic);
            bRet &= SetDataTableColumnsFromDB(_dtInfo, dt);
            bRet &= SetDataTableColumnsFromDB(_dtECUInfo, dt);

            DataTable dtIUPR = new DataTable("OBDIUPR");
            DbLocal.GetEmptyTable(dtIUPR);
            dtIUPR.Columns.Remove("ID");
            dtIUPR.Columns.Remove("WriteTime");
            DbLocal.GetRecords(dtIUPR, whereDic);
            bRet &= SetDataTableColumnsFromDB(_dtIUPR, dtIUPR);

            SetDataTableInfoFromDB(dt);
            SetDataTableECUInfoFromDB(dt);
            SetDataTableIUPRFromDB(dtIUPR);
            // bRet为false的话，会Invoke错误通知事件
            if (bRet) {
                ReadDataFromDBDone?.Invoke();
            }
            dt.Dispose();
        }

        private bool GetCompIgn(DataTable dtIn) {
            bool compIgn = true;
            compIgn = compIgn && dtIn.Rows[0]["CAT_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["HCAT_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["EVAP_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["AIR_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["ACRF_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["O2S_RDY"].ToString() == "不适用";
            compIgn = compIgn && dtIn.Rows[0]["HTR_RDY"].ToString() == "不适用";
            return compIgn;
        }

        private bool GetCompIgnIUPR(DataTable dtIn) {
            bool compIgnIUPR = true;
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["HCCATCOMP"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["NCATCOMP"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["NADSCOMP"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["PMCOMP"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["EGSCOMP"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["EGRCOMP_0B"].ToString() == "-1";
            compIgnIUPR = compIgnIUPR && dtIn.Rows[0]["BPCOMP"].ToString() == "-1";
            return !compIgnIUPR;
        }

        private void CheckCALIDCVN(string strType, string strECUID, string strCALID, string strCVN, CheckResult check) {
            // CALID, CVN 状态: 0 - CALID和CVN均为false，1 - CALID为true，2 - CVN为true，3 - CALID和CVN均为true
            byte status;
            List<byte> ls = new List<byte>();
            Dictionary<string, string> TypeDic = new Dictionary<string, string> { { "Type", strType }, { "ECU_ID", strECUID } };
            DataTable dt = new DataTable("VehicleType");
            DbLocal.GetRecords(dt, TypeDic);
            if (dt.Rows.Count > 0) {
                for (int i = 0; i < dt.Rows.Count; i++) {
                    status = 0;
                    if (dt.Rows[i]["CAL_ID"].ToString() == strCALID) {
                        status |= 0x01;
                    }
                    if (dt.Rows[i]["CVN"].ToString() == strCVN) {
                        status |= 0x02;
                    }
                    ls.Add(status);
                    string strLog = "VehicleType data from database:";
                    strLog += " [Type: " + dt.Rows[i]["Type"];
                    strLog += ", ECU_ID: " + dt.Rows[i]["ECU_ID"];
                    strLog += ", CAL_ID: " + dt.Rows[i]["CAL_ID"];
                    strLog += ", CVN: " + dt.Rows[i]["CVN"] + "]";
                    _obdIfEx.Log.TraceInfo(strLog);
                    if ((status & 0x03) == 3) {
                        break;
                    }
                }
            } else {
                VehicleTypeExist = false;
            }
            if (ls.Count > 0) {
                ls.Sort();
                check.CALID = (ls.Last() & 0x01) == 1;
                check.CVN = (ls.Last() & 0x02) == 2;
            } else {
                check.CALID = false;
                check.CVN = false;
            }
            CALIDCheckResult = CALIDCheckResult && check.CALID;
            CVNCheckResult = CVNCheckResult && check.CVN;
        }

        private void ExportResultFile(DataTable dt) {
            string OriPath = ".\\Configs\\OBD_Result.xlsx";
            string ExportPath = ".\\Export\\" + DateTime.Now.ToLocalTime().ToString("yyyy-MM");
            if (!Directory.Exists(ExportPath)) {
                Directory.CreateDirectory(ExportPath);
            }
            ExportPath += "\\" + StrVIN_IN + "_" + DateTime.Now.ToLocalTime().ToString("yyyyMMdd-HHmmss") + ".xlsx";
            FileInfo fileInfo = new FileInfo(OriPath);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage package = new ExcelPackage(fileInfo, true)) {
                ExcelWorksheet worksheet1 = package.Workbook.Worksheets[0];
                // VIN
                worksheet1.Cells["B2"].Value = dt.Rows[0]["VIN"].ToString();

                // CALID, CVN
                string[] CALIDArray = dt.Rows[0]["CAL_ID"].ToString().Split(',');
                string[] CVNArray = dt.Rows[0]["CVN"].ToString().Split(',');
                for (int i = 0; i < 2; i++) {
                    worksheet1.Cells[3 + i, 2].Value = CALIDArray.Length > i ? CALIDArray[i] : "";
                    worksheet1.Cells[3 + i, 4].Value = CVNArray.Length > i ? CVNArray[i] : "";
                }
                for (int i = 1; i < dt.Rows.Count; i++) {
                    worksheet1.Cells["B5"].Value = dt.Rows[i]["CAL_ID"].ToString().Replace(",", "\n");
                    worksheet1.Cells["D5"].Value = dt.Rows[i]["CVN"].ToString().Replace(",", "\n");
                }

                // moduleID
                string moduleID = GetModuleID(dt.Rows[0]["ECU_NAME"].ToString().Split('-')[0], dt.Rows[0]["ECU_ID"].ToString());
                worksheet1.Cells["E3"].Value = moduleID;
                worksheet1.Cells["B4"].Value += "";
                worksheet1.Cells["D4"].Value += "";
                if (worksheet1.Cells["B4"].Value.ToString().Length > 0 || worksheet1.Cells["D4"].Value.ToString().Length > 0) {
                    if (_obdIfEx.OBDResultSetting.UseECUAcronym) {
                        worksheet1.Cells["E4"].Value = moduleID;
                    } else {
                        worksheet1.Cells["E4"].Value = moduleID;
                    }
                }
                string OtherID = "";
                for (int i = 1; i < dt.Rows.Count; i++) {
                    moduleID = GetModuleID(dt.Rows[i]["ECU_NAME"].ToString().Split('-')[0], dt.Rows[i][1].ToString());
                    OtherID += "," + moduleID;
                }
                worksheet1.Cells["E5"].Value = OtherID.Trim(',');

                // 对校验错误的ECU_ID中的CALID和CVN单元格设置颜色
                foreach (CheckResult check in Checks) {
                    if (worksheet1.Cells["E3"].Value != null && worksheet1.Cells["E3"].Value.ToString() == check.ECUID && check.Index == 0) {
                        if (!check.CALID) {
                            SetCellErrorStyle(worksheet1.Cells["B3"]);
                        }
                        if (!check.CVN) {
                            SetCellErrorStyle(worksheet1.Cells["D3"]);
                        }
                    }
                    if (worksheet1.Cells["E4"].Value != null && worksheet1.Cells["E4"].Value.ToString() == check.ECUID && check.Index == 1) {
                        if (!check.CALID) {
                            SetCellErrorStyle(worksheet1.Cells["B4"]);
                        }
                        if (!check.CVN) {
                            SetCellErrorStyle(worksheet1.Cells["D4"]);
                        }
                    }
                    if (worksheet1.Cells["E5"].Value != null && worksheet1.Cells["E5"].Value.ToString() == check.ECUID && check.Index == 0) {
                        if (!check.CALID) {
                            SetCellErrorStyle(worksheet1.Cells["B5"]);
                        }
                        if (!check.CVN) {
                            SetCellErrorStyle(worksheet1.Cells["D5"]);
                        }
                    }
                }

                // 与OBD诊断仪通讯情况
                worksheet1.Cells["B7"].Value = "通讯成功";

                // 检测结果
                string Result = OBDResult ? "合格" : "不合格";
                Result += DTCResult ? "" : "\n存在DTC故障码";
                Result += ReadinessResult ? "" : "\n就绪状态未完成项超过2项";
                Result += VINResult ? "" : "\nVIN号不匹配";
                Result += CALIDCVNResult ? "" : "\nCALID和CVN数据不完整";
                Result += SpaceResult ? "" : "\nCALID或CVN有多个空格";
                worksheet1.Cells["B8"].Value = Result;

                // 检验员
                worksheet1.Cells["E9"].Value = _obdIfEx.MainSettings.TesterName;

                byte[] bin = package.GetAsByteArray();
                FileInfo exportFileInfo = new FileInfo(ExportPath);
                File.WriteAllBytes(exportFileInfo.FullName, bin);
            }
        }

        private void ExportXmlResult(DataTable dt) {
            string ExportPath = ".\\XmlResult\\" + DateTime.Now.ToLocalTime().ToString("yyyy-MM");
            if (!Directory.Exists(ExportPath)) {
                Directory.CreateDirectory(ExportPath);
            }
            ExportPath += "\\" + StrVIN_IN + "_" + DateTime.Now.ToLocalTime().ToString("yyyyMMdd-HHmmss") + ".xml";
            dt.WriteXml(ExportPath, XmlWriteMode.WriteSchema, true);
        }

        private void SetCellErrorStyle(ExcelRange cell) {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.Red);
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Font.Bold = true;
        }
    }

    public class SetDataTableColumnsErrorEventArgs : EventArgs {
        public string ErrorMsg { get; set; }
    }

    public class CheckResult {
        public string ECUID { get; set; }
        public int Index { get; set; }
        public bool CALID { get; set; }
        public bool CVN { get; set; }

        public CheckResult(string ECUID = "", int Index = 0, bool CALID = true, bool CVN = true) {
            this.ECUID = ECUID;
            this.Index = Index;
            this.CALID = CALID;
            this.CVN = CVN;
        }
    }
}
