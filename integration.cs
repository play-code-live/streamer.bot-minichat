
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
            new string[]
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
            new string[]
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
            new string[]
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
            new string[]
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
            }
        },
        {
            "WASD",
            new string[]
            {
                "Message",
                "Follow",
                "UnFollow",
                "Subscription",
            }
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
            .On(SocketService.eventMessageEventRecieved, delegate (string Event, Dictionary<string, object> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Service") || !Data.ContainsKey("Message") || !Data.ContainsKey("UserName"))
                {
                    Logger.Error("Wrong event structure. Service, Message and UserName are required", jsonData);
                    return;
                }

                string service = Data["Service"].ToString();
                string message = Data["Message"].ToString();
                string user = Data["UserName"].ToString();

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
            .On(SocketService.eventRewardEventRecieved, delegate (string Event, Dictionary<string, object> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Service") || !Data.ContainsKey("RewardName") || !Data.ContainsKey("UserName"))
                {
                    Logger.Error("Wrong event structure. Service, RewardName and UserName are required", jsonData);
                    return;
                }

                string service = Data["Service"].ToString();
                string rewardName = Data["RewardName"].ToString();
                string user = Data["UserName"].ToString();
                string input = Data["RewardInput"].ToString();

                CPH.SetArgument("userName", user);
                CPH.SetArgument("user", user);
                CPH.SetArgument("rawInput", input);
                CPH.SetArgument("rawInputEscaped", input);

                // Register the rest of parameters
                foreach (var datum in Data)
                {
                    CPH.SetArgument(string.Format("minichat.{0}", datum.Key), datum.Value);
                }

                EnsureCustomRewardEventRegistered(service, rewardName);
                TriggerCustomRewardEvent(service, rewardName);
            })
            .On(SocketService.eventLiveEventRecieved, delegate (string Event, Dictionary<string, object> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Service") || !Data.ContainsKey("Type"))
                {
                    Logger.Error("Wrong event structure. Service and Type are required", jsonData);
                    return;
                }
                string service = Data["Service"].ToString();
                string type = Data["Type"].ToString();
                string user = Data["UserName"].ToString();

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
            .On(SocketService.eventConnected, delegate (string Event, Dictionary<string, object> Data)
            {
                Logger.Debug("Connected to the socket. Waiting for the message");
                sendMessage("MiniChat", "🟢 Streamer.bot подключен к MiniChat");
            })
            .On(SocketService.eventReconnected, delegate (string Event, Dictionary<string, object> Data)
            {
                Logger.Debug("Connection to the socket has been restored");
                sendMessage("MiniChat", "🟡 Streamer.bot восстановил подключение");
            })
            .On(SocketService.eventDisconnected, delegate (string Event, Dictionary<string, object> Data)
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

                    Observer.Dispatch(eventDataPair.Key, eventDataPair.Value.ToDictionary());
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error("Error from socket", e.Message);
            Observer.Dispatch(eventDisconnected);
        }

        int updatedSleep = Math.Max(sleep, 1000) * 2;
        Logger.Debug(string.Format("Next attempt in {0} seconds", updatedSleep / 1000));

        return ConnectAndProccess(updatedSleep);
    }

    public class EventTypeLocator
    {
        public static KeyValuePair<string, IDataExtracter> BuildExportableEvent(string rawMessage)
        {
            var result = new KeyValuePair<string, IDataExtracter> ("", null);
            var typeAwareEvent = JsonConvert.DeserializeObject<TypeAwareEvent>(rawMessage);
            if (typeAwareEvent == null) return result;

            var data = (ServiceAwarePayload)typeAwareEvent.Data;
            if (data.Service == "Twitch" || data.Service == "YouTube") return result;

            if (typeAwareEvent.Type == "Live")
            {
                var eventData = JsonConvert.DeserializeObject<LiveEvent>(rawMessage).Data;
                return new KeyValuePair<string, IDataExtracter>(eventLiveEventRecieved, eventData);
            }
            else if (typeAwareEvent.Type == "Message")
            {
                var eventData = JsonConvert.DeserializeObject<MessageEvent>(rawMessage).Data;
                var vkPlayRewardEvent = VKPlayRewardEvent.TryConvertToRewardEvent(eventData);
                if (vkPlayRewardEvent != null)
                    return new KeyValuePair<string, IDataExtracter>(eventRewardEventRecieved, vkPlayRewardEvent);
                return new KeyValuePair<string, IDataExtracter>(eventMessageEventRecieved, eventData);
            }

            return result;
        }
    }

    public class TypeAwareEvent
    {
        public string Type { get; set; }

        public object Data = new ServiceAwarePayload();
    }

    public class ServiceAwarePayload: IDataExtracter
    {
        public string Service { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> { { "Service", Service } };
        }
    }

    public class LiveEvent: TypeAwareEvent
    {
        public new LiveEventPayload Data = new LiveEventPayload();
    }

    public class MessageEvent: TypeAwareEvent
    {
        public new MessageEventPayload Data = new MessageEventPayload();
    }

    public interface IDataExtracter
    {
        Dictionary<string, object> ToDictionary();
    }

    public class MessageEventPayload: ServiceAwarePayload, IDataExtracter
    {
        public string GUID { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }

        public Dictionary<string, string> Avatar = new Dictionary<string, string>();
        public List<MessageKit> MessageKit { get; set; }

        public new Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "Service", Service },
                { "GUID", GUID },
                { "UserID", UserID },
                { "UserName", UserName },
                { "Message", GetMessageText() },
            };
        }

        public string GetMessageText()
        {
            foreach (var kit in MessageKit)
            {
                if (kit.Type == "Text")
                {
                    return kit.Data["Text"].ToString();
                }
            }

            return null;
        }
    }

    public class MessageKit
    {
        public string Type { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    public class VKPlayRewardEvent: IDataExtracter
    {
        public static VKPlayRewardEvent TryConvertToRewardEvent(MessageEventPayload data)
        {
            if (data.Service != "VKPlay" || data.UserName != "ChatBot") return null;
            if (data.MessageKit.Count == 0 || !data.MessageKit[0].Data.ContainsKey("Text")) return null;

            //play_code получает награду: Кинуть флешку за 1\n
            var messageText = data.MessageKit[0].Data["Text"].ToString();
            if (!messageText.Contains("получает награду")) return null;

            var messageSplit = messageText.Split(new string[] { " получает награду: " }, StringSplitOptions.None);
            string userName = messageSplit[0] ?? "Unknown";
            var inputSplit = messageSplit[1].Split('\n');
            var rewardSplit = inputSplit[0].Split(new string[] { " за " }, StringSplitOptions.None);

            return new VKPlayRewardEvent { 
                Username = userName,
                RewardName = rewardSplit[0],
                RewardPrice = Convert.ToInt32(rewardSplit[1]),
                RewardInput = inputSplit[1] ?? "",
            };
        }

        public string Username { get; set; }
        public string RewardName { get; set; }
        public int RewardPrice { get; set; }

        public string RewardInput { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "Service", "VKPlay" },
                { "UserName", Username },
                { "RewardName", RewardName },
                { "RewardPrice", RewardPrice },
                { "RewardInput", RewardInput },
            };
        }
    }

    public class LiveEventPayload: ServiceAwarePayload, IDataExtracter
    {
        public string Type { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }

        public Dictionary<string, string> Avatar = new Dictionary<string, string>();

        public new Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "Type", Type },
                { "UserID", UserID },
                { "UserName", UserName },
            };
        }
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
    public delegate void Handler(string Event, Dictionary<string, object> Data = null);
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
    public void Dispatch(string EventName, Dictionary<string, object> Data = null)
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
