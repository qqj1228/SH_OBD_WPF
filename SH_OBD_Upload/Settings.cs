using LibBase;
using System;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SH_OBD_Upload {
    public class ConfigFile<T> where T : new() {
        public string File_xml { get; }
        public T Data { get; set; }
        public string Name {
            get { return Path.GetFileName(File_xml).Split('.')[0]; }
        }

        public ConfigFile(string xml) {
            File_xml = xml;
        }
    }


    public class Config {
        private readonly Logger _log;
        public ConfigFile<OracleSetting> OracleMES { get; set; }


        public Config(Logger log) {
            _log = log;
            OracleMES = new ConfigFile<OracleSetting>(".\\Configs\\OracleSetting.xml");
            LoadConfig(OracleMES);
        }

        public void LoadConfig<T>(ConfigFile<T> config) where T : new() {
            try {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (FileStream reader = new FileStream(config.File_xml, FileMode.Open)) {
                    config.Data = (T)serializer.Deserialize(reader);
                    reader.Close();
                }
            } catch (Exception ex) {
                _log.TraceError("Using default " + config.Name + " because of failed to load them, reason: " + ex.Message);
                config.Data = new T();
                MessageBox.Show("加载配置文件出错: " + ex.Message, "配置文件", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SaveConfig<T>(ConfigFile<T> config) where T : new() {
            if (config == null || config.Data == null) {
                throw new ArgumentNullException(nameof(config.Data));
            }
            try {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                namespaces.Add(string.Empty, string.Empty);
                using (TextWriter writer = new StreamWriter(config.File_xml)) {
                    xmlSerializer.Serialize(writer, config.Data, namespaces);
                    writer.Close();
                }
            } catch (Exception ex) {
                _log.TraceError("Save " + config.Name + " error, reason: " + ex.Message);
                MessageBox.Show("保存配置文件出错: " + ex.Message, "配置文件", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [Serializable]
    public class OracleSetting {
        public string Host { get; set; }
        public string Port { get; set; }
        public string ServiceName { get; set; }
        public string UserID { get; set; }
        public string PassWord { get; set; }

        public OracleSetting() {
            Host = "192.168.1.49";
            Port = "1521";
            ServiceName = "XE";
            UserID = "c##scott";
            PassWord = "tiger";
        }
    }
}
