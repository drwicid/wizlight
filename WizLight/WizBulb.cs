using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wiz
{
    public enum Scenes
    {
        Ocean = 1,
        Romance,
        Sunset,
        Party,
        Fireplace,
        Cozy,
        Forest,
        Pastel_Colors,
        Wake_up,
        Bedtime,
        Warm_White,
        Daylight,
        Cool_white,
        Night_light,
        Focus,
        Relax,
        True_colors,
        TV_time,
        Plantgrowth,
        Spring,
        Summer,
        Fall,
        Deepdive,
        Jungle,
        Mojito,
        Club,
        Christmas,
        Halloween,
        Candlelight,
        Golden_white,
        Pulse,
        Steampunk
    }

    public class WizBulb
    {
        private const UInt16 DEFAULT_PORT = 38899;

        private IPEndPoint endpoint = null;
        private UdpClient udpclient = null;

        public WizBulb? Connect(IPAddress address, UInt16 port = DEFAULT_PORT)
        {
            WizBulb wizBulb = new WizBulb(address, port);
            return wizBulb;

        }

        public WizBulb(IPAddress address, UInt16 port = DEFAULT_PORT)
        {
            endpoint = new IPEndPoint(address, port);
            udpclient = new UdpClient();

            udpclient.Connect(endpoint);
        }

        public bool SetRGB(ushort r, ushort g, ushort b, ushort c = 0, ushort w = 0, ushort? dimming = null)
        {
            LightStateInput lightStateInput = new LightStateInput();

            lightStateInput.r = r;
            lightStateInput.g = g;
            lightStateInput.b = b;
            lightStateInput.c = c;
            lightStateInput.w = w;
            lightStateInput.dimming = dimming;

            SetPilot(lightStateInput);

            return false;
        }

        public bool SetWhite(UInt16 temp, ushort? brightness = null)
        {
            LightStateInput lightStateInput = new LightStateInput();

            lightStateInput.temp = temp;
            lightStateInput.dimming = brightness;

            SetPilot(lightStateInput);

            return false;
        }

        public bool SetScene(Scenes sceneId, ushort? speed = null, ushort? brightness = null)
        {
            LightStateInput lightStateInput = new LightStateInput();

            lightStateInput.sceneId = sceneId;
            lightStateInput.speed = speed;
            lightStateInput.dimming = brightness;

            SetPilot(lightStateInput);

            return false;
        }

        public bool SetBrightness(ushort brightness)
        {
            LightStateInput lightStateInput = new LightStateInput();
            lightStateInput.dimming = brightness;
            SetPilot(lightStateInput);
            return false;
        }

        public ushort GetBrightness()
        {
            LightStateInput? lightStateInput = GetPilot();

            if (lightStateInput?.dimming != null)
                return lightStateInput.dimming.Value;
            return 0;
        }

        public bool SetPower(bool powerState)
        {
            LightStateInput lightStateInput = new LightStateInput();
            lightStateInput.state = powerState;
            SetPilot(lightStateInput);
            return false;
        }

        public bool GetPower()
        {
            LightStateInput? lightStateInput = GetPilot();

            if (lightStateInput?.state != null)
                return lightStateInput.state.Value;
            return false;
        }

        private bool SetPilot(LightStateInput lightStateInput)
        {
            WizRestSuccess? success = null;
            WizRestError? error = null;

            InvokeUdpRestMethod<LightStateInput, WizRestSuccess>("setPilot", lightStateInput, ref success, ref error);
            return false;
        }

        private LightStateInput? GetPilot()
        {
            LightStateInput? result = null;
            WizRestError? error = null;

            InvokeUdpRestMethod<object, LightStateInput>("getPilot", null, ref result, ref error);
            return result;
        }


        // A Wiz "set" Method accepts a json structure of properties, for example LightStateInput (RGB, white, etc)
        // A Wiz "set" Method returns a json structure of "results":{"success":true} and an "error":{"code":int,"message":"message"} if error
        // So templatized, InvokeUdpRestMethod<LightStateInput, WizRestSuccess>("setPilot", lightStateInput, ref wizRestSuccess, ref wizRestError);
        //
        // A Wiz "get" Method accepts no parameters, parameters = null
        // A Wiz "get" Method returns a json result of "results":{"r":255,"g":0,...} or an "error":{"code":int,"message":"message"} if error
        // So templatized, InvokeUdpRestMethod<object, LightStateInput>("getPilot", null, ref lightStateInput, ref wizRestError);
        private bool InvokeUdpRestMethod<T1, T2>(string method, T1? parameters, ref T2? result, ref WizRestError? error)
        {
            JsonSerializerOptions jsonOptions = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            WizRestRequest<T1> request = new WizRestRequest<T1>();
            WizRestResponse<T2>? response;

            request.method = method;
            request.parameters = parameters;

            string jsonRequest = JsonSerializer.Serialize<WizRestRequest<T1>>(request, jsonOptions);
            udpclient.Send(ASCIIEncoding.ASCII.GetBytes(jsonRequest));
            Console.WriteLine("Request:  {0}", jsonRequest);

            string jsonResponse = ASCIIEncoding.ASCII.GetString(udpclient.Receive(ref endpoint));
            Console.WriteLine("Response: {0}", jsonResponse);
            response = JsonSerializer.Deserialize<WizRestResponse<T2>>(jsonResponse, jsonOptions);

            result = response.result;
            error = response.error;

            return false;
        }
    }

    internal class LightStateInput
    {
        public bool? state { get; set; }
        public Scenes? sceneId { get; set; }
        public ushort? r { get; set; }
        public ushort? g { get; set; }
        public ushort? b { get; set; }
        public ushort? c { get; set; }
        public ushort? w { get; set; }
        public UInt16? temp { get; set; }
        public ushort? dimming { get; set; }
        public ushort? speed { get; set; }
    }

    // Internal Class to Represent the formatted message
    internal class WizRestRequest<T>
    {
        public string method { get; set; }
        [JsonPropertyName("params")]
        public T? parameters { get; set; }
        public WizRestError? error { get; set; }
    }
    
    internal class WizRestResponse<T>
    {
        public string method { get; set; }
        public string env { get; set; }
        public T? result { get; set; }
        public WizRestError? error { get; set; }
    }

    internal class WizRestError
    {
        public Int32 code { get; set; }
        public string message { get; set; }
    }

    internal class WizRestSuccess
    {
        public bool success { get; set; }
    }
}