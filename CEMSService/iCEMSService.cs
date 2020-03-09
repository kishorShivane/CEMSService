using EasyNetQ;
using EasyNetQ.DI;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace CEMSService
{
    public partial class iCEMSService : ServiceBase
    {
        private readonly ConnectionConfiguration _connectionConfiguration = new ConnectionConfiguration();
        private readonly string _mHost = ConfigurationManager.AppSettings.Get("Host");
        private readonly string _mUsername = ConfigurationManager.AppSettings.Get("Username");
        private readonly string _mPassword = ConfigurationManager.AppSettings.Get("Password");
        private readonly string _mSiteControllerQueue = ConfigurationManager.AppSettings.Get("SiteQueue");
        private readonly string m_ReturnQueue = ConfigurationManager.AppSettings.Get("ReturnQueue");
        private ConnectionFactory connectionFactory = new ConnectionFactory();
        private List<string> lstMessages = new List<string>();

        public iCEMSService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            InitAMQPConnectionConfiguration();
            InitAMQPReceive();
        }

        protected override void OnStop()
        {
        }

        private void InitAMQPConnectionConfiguration()
        {
            _connectionConfiguration.Hosts = new List<HostConfiguration> { new HostConfiguration { Host = _mHost } };

            _connectionConfiguration.UserName = _mUsername;
            _connectionConfiguration.Password = _mPassword;
            _connectionConfiguration.Timeout = 30;
        }

        private void InitAMQPReceive()
        {
            var bus = RabbitHutch.CreateBus(_connectionConfiguration, registerServices).Advanced;
            var queue = bus.QueueDeclare(_mSiteControllerQueue, false, false);
            bus.Consume(queue, (body, properties, info) => ProcessMessage(body));

            LogMessage(string.Format("Connected and listening on Host: {0} Queue: {1}", _mHost, _mSiteControllerQueue));
        }

        private void AMQPSend(string queue, object message)
        {
            using (var bus = RabbitHutch.CreateBus(_connectionConfiguration, registerServices))
            {
                var serializedMessage = SerializeMessage(message);
                LogMessage("## SENDING: " + message.GetType().ToString());
                bus.Send(queue, serializedMessage);
            }
        }

        public void ProcessMessage(byte[] body)
        {

            var message = Encoding.UTF8.GetString(body);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(message);
            XmlNamespaceManager nm = new XmlNamespaceManager(doc.NameTable);
            nm.AddNamespace("s", "http://www.w3.org/2003/05/soap-envelope");
            nm.AddNamespace("a", "http://www.w3.org/2005/08/addressing");

            XmlNode msgBody = doc.SelectSingleNode("/s:Envelope/s:Body", nm);

            //MessageFrameConfigurationElement messageConfig = MessageFrameConfiguration.GetMessageConfigurationElement(msgBody.InnerXml);

            XmlSerializer serializer = new XmlSerializer(messageConfig.Type);
            StringReader reader = new StringReader(msgBody.InnerXml);
            var typedMessage = serializer.Deserialize(reader);

            InvokeHandlerMethod(this.GetType().ToString(), messageConfig.HandlerMethod, typedMessage);

            //LogMessage(string.Format("Received message: '{0}'", msgBody.InnerXml));

        }

        

        private void LogMessage(string message)
        {           
                lstMessages.Add(message);
        }

        private static void registerServices(IServiceRegister obj)
        {
            //throw new NotImplementedException();
        }

        public void InvokeHandlerMethod(string typeName, string methodName, object message)
        {
            Type calledType = Type.GetType(typeName);
            MethodInfo method = calledType.GetMethod(methodName);
            method.Invoke(this, new object[] { message });
            //calledType.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, null, new Object[] { message});
        }

        public static string SerializeMessage(object message)
        {

            var stringwriter = new System.IO.StringWriter();
            var serializer = new XmlSerializer(message.GetType());
            serializer.Serialize(stringwriter, message);
            return stringwriter.ToString();
        }

    }
}
