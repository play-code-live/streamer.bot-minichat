
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

///----------------------------------------------------------------------------
///   Module:     MiniChat Integration
///   Author:     play_code (https://twitch.tv/play_code)
///   Email:      info@play-code.live
///   Repository: https://github.com/play-code-live/streamer.bot-minichat
///----------------------------------------------------------------------------
public class CPHInline
{
    private readonly Dictionary<string, string[]> prebuildEvents = new Dictionary<string, string[]>
    {
        {
            "VKPlay",
            new []
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
                "Raid",
            }
        },
        {
            "Boosty",
            new []
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
                "GiftSubscription",
            }
        },
        {
            "Trovo",
            new []
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
                "GiftSubscription",
                "Reward",
                "Raid",
            }
        },
        {
            "VK",
            new []
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
            }
        },
        {
            "WASD",
            new []
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
            }
        },
        {
            "DonationAlerts",
            new [] { "Donation" }
        },
        {
            "DonatePay",
            new [] { "Donation" }
        },
        {
            "DonateStream",
            new [] { "Donation" }
        },
    };

    private PrefixedLogger Logger { get; set; }

    private SocketService Socket { get; set; }
    private ApiService ApiService { get; set; }

    private const string keyEventCollection = "minichat.eventCollection";
    private const string keyRewardEventCollection = "minichat.rewardEventCollection";
    private const string keyConnectionPort = "minichat.connection.port";

    public void Init()
    {
        int Port = CPH.GetGlobalVar<int>(keyConnectionPort);
        Port = Port == 0 ? SocketService.DefaultPort : Port;
        Logger = new PrefixedLogger(CPH);
        Socket = new SocketService(new EventObserver(), Logger, Port);
        ApiService = new ApiService(Logger, Port);

        foreach (var eventGroup in prebuildEvents)
        {
            foreach (var eventType in eventGroup.Value)
            {
                RegisterCustomEvent(eventGroup.Key, eventType);
            }
        }

        // Allows us not to add each event manually, but catch and memorize it on the fly
        LoadAndRegisterCustomEvents();
        LoadAndRegisterCustomRewardEvents();
    }

    public void Dispatch()
    {
        Socket.Close();
    }

    public bool ForceQuit()
    {
        Socket.Close();

        return true;
    }

    public bool Execute()
    {
        return IntegrationLoop();
    }

    public bool Speak()
    {
        if (!args.ContainsKey("message"))
        {
            Logger.Error("Message argument is required for the speech");
            return false;
        }

        string message = args["message"].ToString();
        string voice = ApiService.availableVoices[0];
        if (args.ContainsKey("voice"))
            voice = args["voice"].ToString();

        ApiService.Speak(message, voice);

        return true;
    }

    public bool SendMessageSystem()
    {
        return sendMessage("MiniChat");
    }

    public bool SendMessageGoodGame()
    {
        return sendMessage("GoodGame");
    }

    public bool SendMessageVK()
    {
        return sendMessage("VK");
    }

    public bool SendMessageWASD()
    {
        return sendMessage("WASD");
    }

    public bool SendMessageSteam()
    {
        return sendMessage("Steam");
    }

    public bool SendMessageFacebook()
    {
        return sendMessage("Facebook");
    }

    public bool SendMessageOK()
    {
        return sendMessage("OK");
    }

    public bool SendMessageTrovo()
    {
        return sendMessage("Trovo");
    }

    public bool SendMessageRutube()
    {
        return sendMessage("Rutube");
    }

    public bool SendMessageTelegram()
    {
        return sendMessage("Telegram");
    }

    public bool SendMessageVkPlay()
    {
        return sendMessage("VKPlay");
    }

    public bool SendMessageBoosty()
    {
        return sendMessage("Boosty");
    }

    private bool sendMessage(string service)
    {
        if (!args.ContainsKey("message"))
        {
            Logger.Error("Message argument is required for the speech");
            return false;
        }
        string message = args["message"].ToString();

        return sendMessage(service, message);
    }

    private bool sendMessage(string service, string message)
    {
        string username = args.ContainsKey("broadcastUserName") ? args["broadcastUserName"].ToString() : "System";

        ApiService.SendMessage(message, service, username);

        return true;
    }

    private bool IntegrationLoop()
    {
        Socket
            .On(SocketService.eventMessageEventRecieved, delegate (string Event, Dictionary<string, string> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Data.Service") || !Data.ContainsKey("Data.MessageKit.0.Data.Text") || !Data.ContainsKey("Data.UserName"))
                {
                    Logger.Error("Wrong event structure. Service, Message and UserName are required", jsonData);
                    return;
                }

                string service = Data["Data.Service"].ToString();
                string message = Data["Data.MessageKit.0.Data.Text"].ToString(); //TODO Refactor that shit
                string user = Data["Data.UserName"].ToString();

                CPH.SetArgument("userName", user);
                CPH.SetArgument("user", user);
                CPH.SetArgument("message", message);

                // Register the rest of parameters
                foreach (var datum in Data)
                {
                    CPH.SetArgument(string.Format("minichat.{0}", datum.Key), datum.Value);
                }

                EnsureCustomEventRegistered(service, "Message");
                TriggerCustomEvent(service, "Message");
            })
            .On(SocketService.eventRewardEventRecieved, delegate (string Event, Dictionary<string, string> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Data.Service") || !Data.ContainsKey("Data.Name") || !Data.ContainsKey("Data.UserName"))
                {
                    Logger.Error("Wrong event structure. Service, RewardName and UserName are required", jsonData);
                    return;
                }

                string service = Data["Data.Service"].ToString();
                string rewardName = Data["Data.Name"].ToString();
                string user = Data["Data.UserName"].ToString();
                string input = Data["Data.Message"].ToString();

                CPH.SetArgument("userName", user);
                CPH.SetArgument("user", user);
                CPH.SetArgument("rawInput", input);
                CPH.SetArgument("rawInputEscaped", input);
                CPH.SetArgument("message", input);

                // Register the rest of parameters
                foreach (var datum in Data)
                {
                    CPH.SetArgument(string.Format("minichat.{0}", datum.Key), datum.Value);
                }

                EnsureCustomRewardEventRegistered(service, rewardName);
                TriggerCustomRewardEvent(service, rewardName);
            })
            .On(SocketService.eventLiveEventRecieved, delegate (string Event, Dictionary<string, string> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Data.Service") || !Data.ContainsKey("Data.Type") || !Data.ContainsKey("Data.UserName"))
                {
                    Logger.Error("Wrong event structure. Service, UserName and Type are required", jsonData);
                    return;
                }
                string service = Data["Data.Service"].ToString();
                string type = Data["Data.Type"].ToString();
                string user = Data["Data.UserName"].ToString();

                // These two are the most important and stable
                CPH.SetArgument("userName", user);
                CPH.SetArgument("user", user);

                // Register the rest of parameters
                foreach (var datum in Data)
                {
                    CPH.SetArgument(string.Format("minichat.{0}", datum.Key), datum.Value.ToString());
                }

                EnsureCustomEventRegistered(service, type);
                TriggerCustomEvent(service, type);
            })
            .On(SocketService.eventConnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Connected to the socket. Waiting for the message");
                sendMessage("MiniChat", "🟢 Streamer.bot подключен к MiniChat");
            })
            .On(SocketService.eventReconnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Connection to the socket has been restored");
                sendMessage("MiniChat", "🟡 Streamer.bot восстановил подключение");
            })
            .On(SocketService.eventDisconnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Disconnected from the socket");
                sendMessage("MiniChat", "🔴 Streamer.bot потерял соединение");
            });

        Logger.Debug("Ready to start socket service");
        Socket.Start();
        Logger.Debug("Socket service has stoped his work");
        return true;
    }

    private void TriggerCustomEvent(string service, string type)
    {
        string customEventName = string.Format("MiniChat.{0}.{1}", service, type);
        Logger.Debug("Triggering", customEventName);
        CPH.TriggerCodeEvent(customEventName);
    }

    private void TriggerCustomRewardEvent(string service, string rewardName)
    {
        string customEventName = string.Format("MiniChat.{0}.Reward.{1}", service, rewardName);
        Logger.Debug("Triggering", customEventName);
        CPH.TriggerCodeEvent(customEventName);
    }

    private void RegisterCustomEvent(string service, string type)
    {
        string eventName = string.Format("{0}: {1}", service, type);
        string eventCode = string.Format("MiniChat.{0}.{1}", service, type);
        CPH.RegisterCustomTrigger(eventName, eventCode, new[] { "Minichat", service });
    }

    private void RegisterCustomRewardEvent(string service, string rewardName)
    {
        string eventName = string.Format("{0}: {1}", service, rewardName);
        string eventCode = string.Format("MiniChat.{0}.Reward.{1}", service, rewardName);
        CPH.RegisterCustomTrigger(eventName, eventCode, new[] { "Minichat", service, "Rewards" });
    }

    private void EnsureCustomRewardEventRegistered(string service, string rewardName)
    {
        string memorizedRewardEvents = CPH.GetGlobalVar<string>(keyRewardEventCollection);
        if (string.IsNullOrEmpty(memorizedRewardEvents))
            memorizedRewardEvents = "[]";

        var eventCollection = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(memorizedRewardEvents);
        if (IsCustomEventMemorized(eventCollection, service, rewardName))
            return;

        eventCollection.Add(new KeyValuePair<string, string>(service, rewardName));

        CPH.SetGlobalVar(keyRewardEventCollection, JsonConvert.SerializeObject(eventCollection));
        RegisterCustomRewardEvent(service, rewardName);
    }

    private void EnsureCustomEventRegistered(string service, string type)
    {
        string memorizedEvents = CPH.GetGlobalVar<string>(keyEventCollection);
        if (string.IsNullOrEmpty(memorizedEvents))
            memorizedEvents = "[]";

        var eventCollection = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(memorizedEvents);
        if (IsCustomEventMemorized(eventCollection, service, type))
            return;

        eventCollection.Add(new KeyValuePair<string, string>(service, type));

        CPH.SetGlobalVar(keyEventCollection, JsonConvert.SerializeObject(eventCollection));
        RegisterCustomEvent(service, type);
    }

    private bool IsCustomEventMemorized(List<KeyValuePair<string,string>> collection, string service, string type)
    {
        foreach (var eventData in collection)
        {
            if (eventData.Key == service && eventData.Value == type)
                return true;
        }

        return false;
    }

    private void LoadAndRegisterCustomEvents()
    {
        List<KeyValuePair<string, string>> eventCollection = new List<KeyValuePair<string, string>>();
        string memorizedEvents = CPH.GetGlobalVar<string>(keyEventCollection);
        if (!string.IsNullOrEmpty(memorizedEvents))
            eventCollection = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(memorizedEvents);

        foreach (var eventPair in eventCollection)
        {
            RegisterCustomEvent(eventPair.Key, eventPair.Value);
        }
    }

    private void LoadAndRegisterCustomRewardEvents()
    {
        List<KeyValuePair<string, string>> eventCollection = new List<KeyValuePair<string, string>>();
        string memorizedRewardEvents = CPH.GetGlobalVar<string>(keyRewardEventCollection);
        if (!string.IsNullOrEmpty(memorizedRewardEvents))
            eventCollection = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(memorizedRewardEvents);

        foreach (var eventPair in eventCollection)
        {
            RegisterCustomRewardEvent(eventPair.Key, eventPair.Value);
        }
    }
}

public class SocketService
{
    public const int DefaultPort = 4848;
    private int Port { get; set; }
    private ClientWebSocket Socket { get; set; }
    private PrefixedLogger Logger { get; set; }

    private EventObserver Observer { get; set; }

    private bool IsForceClosed = false;

    private const int BufferSize = 3072;

    public const string eventConnected = "socket.connected";
    public const string eventDisconnected = "socket.disconnected";
    public const string eventReconnected = "socket.reconnected";
    public const string eventLiveEventRecieved = "socket.event.live.recieved";
    public const string eventMessageEventRecieved = "socket.event.message.recieved";
    public const string eventRewardEventRecieved = "socket.event.reward.recieved";

    public SocketService(EventObserver observer, PrefixedLogger logger, int port = DefaultPort)
    {
        Logger = logger;
        Observer = observer;
        Port = port;
    }

    private string getUrl()
    {
        return string.Format("ws://localhost:{0}/Chat", Port);
    }

    public SocketService On(string EventName, EventObserver.Handler handler)
    {
        Observer.Subscribe(EventName, handler);

        return this;
    }

    public Task Start()
    {
        return ConnectAndProccess();
    }
    public void Close()
    {
        try
        {
            if (Socket == null)
                return;
            IsForceClosed = true;
            Socket.Abort();
            Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            Socket = null;
        }
        catch (Exception) { }

    }
    private Task ConnectAndProccess(int sleep = 0)
    {
        Thread.Sleep(sleep);
        try
        {
            Socket = new ClientWebSocket();
            Socket.ConnectAsync(new Uri(getUrl()), CancellationToken.None).GetAwaiter().GetResult();

            var buf = new ArraySegment<byte>(new byte[BufferSize]);
            
            if (Socket.State == WebSocketState.Open)
            {
                Observer.Dispatch(sleep == 0 ? eventConnected : eventReconnected);
                sleep = 0;
            }
        
            while (Socket.State == WebSocketState.Open)
            {
                var result = Socket.ReceiveAsync(buf, CancellationToken.None).GetAwaiter().GetResult();
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    Logger.Debug("Connection has been closed by the socket");
                    Observer.Dispatch(eventDisconnected);
                }
                else
                {
                    string rawMessage = Encoding.UTF8.GetString(buf.Array, 0, result.Count);
                    var eventDataPair = EventTypeLocator.BuildExportableEvent(rawMessage);
                    if (eventDataPair.Value == null) continue;

                    Observer.Dispatch(eventDataPair.Key, eventDataPair.Value);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error("Error from socket", e.Message);
            Observer.Dispatch(eventDisconnected);
        }

        if (IsForceClosed)
        {
            return null;
        }

        int updatedSleep = Math.Max(sleep, 1000) * 2;
        Logger.Debug(string.Format("Next attempt in {0} seconds", updatedSleep / 1000));
        return ConnectAndProccess(updatedSleep);
    }

    public class EventTypeLocator
    {
        public static KeyValuePair<string, Dictionary<string, string>> BuildExportableEvent(string rawMessage)
        {
            var result = new KeyValuePair<string, Dictionary<string, string>> ("",null);
            var rootEvent = JsonConvert.DeserializeObject<RootEvent>(rawMessage);
            if (rootEvent == null) return result;

            string service = rootEvent.Data.ContainsKey("Service") ? rootEvent.Data["Service"].ToString() : null;
            if (service != null && (service == "Twitch" || service == "YouTube")) return result;

            var data = InlineRootEvent(rootEvent);
            var vkPlayRewardEvent = TryConvertToVkPlayRewardEvent(data);
            if (vkPlayRewardEvent != null) data = vkPlayRewardEvent;

            string targetEventCode = "";
            switch (data["Type"])
            {
                case "Live":
                    targetEventCode = data["Data.Type"]?.ToString() == "Reward" ? eventRewardEventRecieved : eventLiveEventRecieved;
                    break;
                case "Message":
                    targetEventCode = eventMessageEventRecieved;
                    break;
            }

            if (string.IsNullOrEmpty(targetEventCode))
                return result;

            return new KeyValuePair<string, Dictionary<string, string>>(targetEventCode, data);
        }

        private static Dictionary<string, string> TryConvertToVkPlayRewardEvent(Dictionary<string, string> data)
        {
            if (data["Type"] != "Message" || data["Data.Service"]?.ToString() != "VKPlay" || data["Data.UserName"]?.ToString() != "ChatBot") return null;
            string messageText = "";
            for (int i = 0; data.ContainsKey(string.Format("Data.MessageKit.{0}.Type", i)); i++)
            {
                if (data[string.Format("Data.MessageKit.{0}.Type", i)]?.ToString() != "Text")
                    continue;
                messageText = data[string.Format("Data.MessageKit.{0}.Data.Text", i)]?.ToString();
                break;
            }

            if (string.IsNullOrEmpty(messageText)) return null;
            //play_code получает награду: Кинуть флешку за 1\n

            if (!messageText.Contains("получает награду")) return null;

            var messageSplit = messageText.Split(new string[] { " получает награду: " }, StringSplitOptions.None);
            string userName = messageSplit[0] ?? "Unknown";
            var inputSplit = messageSplit[1].Split('\n');
            var rewardSplit = inputSplit[0].Split(new string[] { " за " }, StringSplitOptions.None);

            return new Dictionary<string, string>
            {
                { "Type", "Live" },
                { "Data.Type", "Reward" },
                { "Data.CurrencyType", "VKPlayPoints" },
                { "Data.Name", rewardSplit[0] },
                { "Data.UserName", messageSplit[0] },
                { "Data.Price", rewardSplit[1] },
                { "Data.Count", "1" },
                { "Data.Amount", rewardSplit[1] },
                { "Data.Message", inputSplit[1] ?? "" },
                { "Data.Service", data["Data.Service"] },
                { "Data.GUID", data["Data.GUID"] ?? "" },
                { "Data.Date", data["Data.Date"] ?? "" },
            };
        }

        private static Dictionary<string, string> InlineRootEvent(RootEvent e)
        {
            var result = new Dictionary<string, string>
            {
                { "Type", e.Type },
            };

            InlineRootDictionaryData("Data", e.Data, ref result);

            return result;
        }

        private static void InlineRootDictionaryData(string prefix, Dictionary<string, object> data, ref Dictionary<string, string> result)
        {
            foreach (var item in data)
            {
                InlineObject(string.Format("{0}.{1}", prefix, item.Key), item.Value, ref result);
            }
        }

        private static void InlineObject(string key, object data, ref Dictionary<string, string> result)
        {
            if (data == null)
                return;

            var listStrData = data as List<string>;
            if (listStrData != null)
            {
                for (int i = 0; i < listStrData.Count; i++)
                    result.Add(string.Format("{0}.{1}", key, i), listStrData[i]);
                return;
            }

            var stringData = data as string;
            if (stringData != null)
            {
                result.Add(key, stringData);
                return;
            }

            if (data is Newtonsoft.Json.Linq.JArray jArrayData)
                data = jArrayData.ToObject<List<object>>();
            var listObjData = data as List<object>;
            if (listObjData != null)
            {
                for (int i = 0; i < listObjData.Count; i++)
                    InlineObject(string.Format("{0}.{1}", key, i), listObjData[i], ref result);
                return;
            }

            if (data is Newtonsoft.Json.Linq.JObject jObjectData)
                data = jObjectData.ToObject<Dictionary<string, object>>();
            var dictData = data as Dictionary<string, object>;
            if (dictData != null)
            {
                InlineRootDictionaryData(key, (Dictionary<string, object>)data, ref result);
                return;
            }

            if (data is DateTime dateData)
                result.Add(key, dateData.ToString());
            else if (data is Boolean boolData)
                result.Add(key, boolData ? "true" : "false");
            else if (data is double doubleData)
                result.Add(key, doubleData.ToString());
            else if (data is int intData)
                result.Add(key, intData.ToString());
        }

    }

    public class RootEvent
    {
        public string Type { get; set; }

        public Dictionary<string, object> Data = new Dictionary<string, object>();
    }
}

public class ApiService
{
    public const int DefaultPort = 4848;
    private int Port { get; set; }

    public readonly List<string> availableVoices = new List<string> {
        "Alice",
        "Marusia",
        "Maxim",
        "Tatyana",
        "Svetlana",
        "Dmitry",
        "Ermilov",
        "Zahar",
        "Jane",
        "Alyss",
        "Omazh",
        "Oksana",
    };

    private PrefixedLogger Logger { get; set; }
    private HttpClient Client { get; set; }

    public ApiService(PrefixedLogger logger, int port = DefaultPort)
    {
        Logger = logger;
        Client = new HttpClient();
        Port = port;
    }

    private string getUrl()
    {
        return string.Format("http://localhost:{0}/api", Port);
    }

    public void Speak(string message, string voice)
    {
        Logger.Debug("Got speach request", voice, message);
        if (!availableVoices.Contains(voice))
            throw new Exception("Unavailable voice requested");

        var request = new Request()
        {
            Type = "Speak",
            Data = new Dictionary<string, string>()
            {
                { "Voice", voice },
                { "Message", message },
            }
        };

        Client.POST(getUrl(), JsonConvert.SerializeObject(request));
    }

    public void SendMessage(string message, string service, string username)
    {
        Logger.Debug("Got send message request", service, message);
        var request = new Request()
        {
            Type = "Message",
            Data = new Dictionary<string, string>
            {
                { "Service", service },
                { "UserName", username },
                { "Message", message },
            }
        };

        Client.POST(getUrl(), JsonConvert.SerializeObject(request));
    }

    private class Request
    {
        public string Type { get; set; }
        public object Data { get; set; }
    }
}

public class HttpClient
{
    public string POST(string url, string payload)
    {
        return Perform(WebRequestMethods.Http.Post, url, payload);
    }
    public string POST(string url, Dictionary<string, string> payload)
    {
        return Perform(WebRequestMethods.Http.Post, url, payload);
    }
    public string POST(string endpoint)
    {
        return POST(endpoint, new Dictionary<string, string>());
    }

    private string Perform(string method, string url, string jsonPayload)
    {
        HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
        webRequest.Method = method;
        webRequest.ContentType = "application/json";

        if (jsonPayload != string.Empty)
        {
            byte[] requestBytes = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.ContentLength = requestBytes.Length;
            Stream requestStream = webRequest.GetRequestStream();
            requestStream.Write(requestBytes, 0, requestBytes.Length);
            requestStream.Close();
        }


        var response = (HttpWebResponse)webRequest.GetResponse();
        string json = "";
        using (Stream respStr = response.GetResponseStream())
        {
            using (StreamReader rdr = new StreamReader(respStr, Encoding.UTF8))
            {
                json = rdr.ReadToEnd();
                rdr.Close();
            }
        }

        return json;
    }
    private string Perform(string method, string url, Dictionary<string, string> payload)
    {
        string payloadString = "";
        if (payload.Count > 0)
            payloadString = JsonConvert.SerializeObject(payload);

        return Perform(method, url, payloadString);
    }
}

public class EventObserver
{
    public delegate void Handler(string Event, Dictionary<string, string> Data = null);
    private Dictionary<string, List<Handler>> Handlers { get; set; }

    public EventObserver()
    {
        Handlers = new Dictionary<string, List<Handler>>();
    }
    public EventObserver Subscribe(string EventName, Handler handler)
    {
        if (!Handlers.ContainsKey(EventName))
            Handlers.Add(EventName, new List<Handler>());

        Handlers[EventName].Add(handler);
        return this;
    }
    public void Dispatch(string EventName, Dictionary<string, string> Data = null)
    {
        if (!Handlers.ContainsKey(EventName) || Handlers[EventName].Count == 0)
            return;

        foreach (var handler in Handlers[EventName])
        {
            handler(EventName, Data);
        }
    }
}

public class PrefixedLogger
{
    private IInlineInvokeProxy cph { get; set; }
    private const string Prefix = "-- Mini Chat:";

    public PrefixedLogger(IInlineInvokeProxy _CPH)
    {
        cph = _CPH;
    }
    public void WebError(WebException e)
    {
        var response = (HttpWebResponse)e.Response;
        var statusCodeResponse = response.StatusCode;
        int statusCodeResponseAsInt = ((int)response.StatusCode);
        Error("WebException with status code " + statusCodeResponseAsInt.ToString(), statusCodeResponse);
    }
    public void Error(string message)
    {
        message = string.Format("{0} {1}", Prefix, message);
        cph.LogWarn(message);
    }
    public void Error(string message, params object[] additional)
    {
        string finalMessage = message;
        foreach (var line in additional)
        {
            finalMessage += ", " + line;
        }
        Error(finalMessage);
    }
    public void Debug(string message)
    {
        message = string.Format("{0} {1}", Prefix, message);
        cph.LogDebug(message);
    }
    public void Debug(string message, params object[] additional)
    {
        string finalMessage = message;
        foreach (var line in additional)
        {
            finalMessage += ", " + line;
        }
        Debug(finalMessage);
    }
}
