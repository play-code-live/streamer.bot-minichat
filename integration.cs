
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
    private PrefixedLogger Logger { get; set; }

    private SocketService Socket { get; set; }
    private ApiService ApiService { get; set; }

    private const string keyEventCollection = "minichat.eventCollection";

    public void Init()
    {
        Logger = new PrefixedLogger(CPH);
        Socket = new SocketService(new EventObserver(), Logger);
        ApiService = new ApiService(Logger);

        var prebuildEvents = new Dictionary<string, string[]>()
        {
            { 
                "VKPlay", 
                new string[] 
                {
                    "Follow",
                    "Subscription",
                } 
            },
            {
                "Boosty",
                new string[]
                {
                    "Follow",
                }
            },
            {
                "Trovo",
                new string[]
                {
                    "Follow",
                    "Subscription",
                    "Raid",
                }
            }
        };

        foreach (var eventGroup in prebuildEvents)
        {
            foreach (var eventType in eventGroup.Value)
            {
                RegisterCustomEvent(eventGroup.Key, eventType);
            }
        }

        // Allows us not to add each event manually, but catch and memorize it on the fly
        LoadAndRegisterCustomEvents();
    }

    public void Dispatch()
    {
        Socket.Close();
    }

    public bool Speak()
    {
        if (!args.ContainsKey("message"))
        {
            Logger.Error("Message argument is required for the speech");
            return false;
        }

        string message = args["message"].ToString();
        string voice = ApiService.Voice.Alice;
        if (args.ContainsKey("voice"))
            voice = args["voice"].ToString();

        ApiService.Speak(message, voice);

        return true;
    }

    public bool Execute()
    {
        Socket
            .On(SocketService.eventRecieved, delegate (string Event, Dictionary<string, string> Data)
            {
                var jsonData = JsonConvert.SerializeObject(Data);
                if (!Data.ContainsKey("Service") || !Data.ContainsKey("Type"))
                {
                    Logger.Error("Wrong event structure. Service and Type are required", jsonData);
                    return;
                }
                string service = Data["Service"];
                string type = Data["Type"];

                // These two are the most important and stable
                CPH.SetArgument("userName", Data["UserName"]);
                CPH.SetArgument("user", Data["UserName"]);

                // Register the rest of parameters
                foreach (var datum in Data)
                {
                    CPH.SetArgument(string.Format("minichat.{0}", datum.Key), datum.Value);
                }

                EnsureCustomEventRegistered(service, type);
                TriggerCustomEvent(service, type);
            })
            .On(SocketService.eventConnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Connected to the socket. Waiting for the message");
            })
            .On(SocketService.eventReconnected, delegate (string Event, Dictionary<string, string> Data)
            {
                Logger.Debug("Connection to the socket has been restored");
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

    private void RegisterCustomEvent(string service, string type)
    {
        string eventName = string.Format("{0}: {1}", service, type);
        string eventCode = string.Format("MiniChat.{0}.{1}", service, type);
        CPH.RegisterCustomTrigger(eventName, eventCode, new[] { "Minichat", service });
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
}

public class SocketService
{
    private const string host = "ws://localhost:4848/Chat"; //TODO: Grab from args or globals
    private ClientWebSocket Socket { get; set; }
    private PrefixedLogger Logger { get; set; }

    private EventObserver Observer { get; set; }

    private const int BufferSize = 3072;

    public const string eventConnected = "socket.connected";
    public const string eventDisconnected = "socket.disconnected";
    public const string eventReconnected = "socket.reconnected";
    public const string eventRecieved = "socket.event.recieved";

    public SocketService(EventObserver observer, PrefixedLogger logger)
    {
        Logger = logger;
        Observer = observer;
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
            Socket.ConnectAsync(new Uri(host), CancellationToken.None).GetAwaiter().GetResult();

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
                    var typeAwareEvent = JsonConvert.DeserializeObject<TypeAwareEvent>(rawMessage);
                    if (typeAwareEvent == null || typeAwareEvent.Type != "Live")
                        continue;

                    var eventData = JsonConvert.DeserializeObject<LiveEvent>(rawMessage).Data;
                    Logger.Debug("New Event", eventData.Type, eventData.Service, eventData.UserName);
                       
                    Observer.Dispatch(eventRecieved, new Dictionary<string, string>()
                    {
                        { "Service", eventData.Service },
                        { "Type", eventData.Type },
                        { "UserName", eventData.UserName }
                    });
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

    public class TypeAwareEvent
    {
        public string Type { get; set; }
    }

    public class LiveEvent: TypeAwareEvent
    {
        public LiveEventPayload Data = new LiveEventPayload();
    }

    public class LiveEventPayload: TypeAwareEvent
    {
        public string Service { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }

        public Dictionary<string, string> Avatar = new Dictionary<string, string>();
    }
}

public class ApiService
{
    private const string hostname = "http://localhost:4848/api";

    public static class Voice
    {
        public const string
            Maxim = "Maxim",
            Tatyana = "Tatyana",
            Alice = "Alice",
            Marusia = "Marusia",
            Svetlana = "Svetlana",
            Dmitry = "Dmitry",
            Ermilov = "Ermilov",
            Zahar = "Zahar",
            Jane = "Jane",
            Alyss = "Alyss",
            Omazh = "Omazh",
            Oksana = "Oksana";
    }

    private PrefixedLogger Logger { get; set; }
    private HttpClient Client { get; set; }

    public ApiService(PrefixedLogger logger)
    {
        Logger = logger;
        Client = new HttpClient();
    }

    public void Speak(string message, string voice)
    {
        Logger.Debug("Got speach request", voice, message);

        var request = new SpeakRequest()
        {
            Data = new SpeakRequestData()
            {
                Voice = voice,
                Message = message
            }
        };

        Client.POST(hostname, JsonConvert.SerializeObject(request));
    }

    private class SpeakRequest
    {
        public string Type = "Speak";
        public SpeakRequestData Data = new SpeakRequestData();
    }

    private class SpeakRequestData
    {
        public string Voice { get; set; }
        public string Message { get; set; }
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
        this.cph = _CPH;
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
