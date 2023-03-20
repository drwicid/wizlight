using System.Diagnostics;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

using Wiz;

enum PowerState
{
    off,
    on,
    toggle
}

enum BrightnessActions
{
    set,
    plus,
    minus
}

class WizLightSharp
{

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    internal static void AttachConsoleForCommandLineMode()
    {
        // redirect console output to parent process;
        // must be before any calls to Console.WriteLine()
        AttachConsole(ATTACH_PARENT_PROCESS);
    }
    public static void Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(System.Console.Out));
        Trace.AutoFlush = true;
        Trace.Indent();

        //AttachConsoleForCommandLineMode();
        // Root command and global options
        var rootCommand = new RootCommand("Wiz Lightbulb Remote Control App");
        var addressArgument = new Argument<IPAddress>(
            name: "address",
            description: "Network address of the bulb",
            parse: result =>
            {
                string? hostOrAddress = result.Tokens.Single().Value;
                try
                {
                    return Dns.GetHostAddresses(hostOrAddress).First();
                }
                catch (Exception e)
                {
                    result.ErrorMessage = e.Message;
                }
                return null;
            });

        var portNumberOption = new Option<ushort?>(new[] { "--port", "-p" }, "Port number of the bulb");
        var brightnessOption = new Option<ushort?>(new[] { "--brightness", "-b" }, "Brightness of the bulb");

        rootCommand.AddArgument(addressArgument);
        rootCommand.AddGlobalOption(portNumberOption);
        rootCommand.AddGlobalOption(brightnessOption);

        // Color command
        var colorCommand = new Command("color", "Change the color");
        var redArgument = new Argument<ushort>("red", "Red Value (0 - 255)");
        var greenArgument = new Argument<ushort>("green", "Green Value (0 - 255)");
        var blueArgument = new Argument<ushort>("blue", "Blue Value (0 - 255)");
        var coldArgument = new Argument<ushort>("cold", "Cold white value (0 - 255)");
        var warmArgument = new Argument<ushort>("warm", "Warm white value (0 - 255)");
        coldArgument.SetDefaultValue(0);
        warmArgument.SetDefaultValue(0);

        rootCommand.AddCommand(colorCommand);
        colorCommand.AddArgument(redArgument);
        colorCommand.AddArgument(greenArgument);
        colorCommand.AddArgument(blueArgument);
        colorCommand.AddArgument(coldArgument);
        colorCommand.AddArgument(warmArgument);
        colorCommand.AddOption(brightnessOption);

        // Scene command
        var sceneCommand = new Command("scene", "Change the scene");
        var sceneIdArgument = new Argument<Scenes>("name", "Scene name");
        var speedOption = new Option<ushort?>(new[] { "--speed", "-s" }, "Scene speed (10-200)");

        rootCommand.Add(sceneCommand);
        sceneCommand.AddArgument(sceneIdArgument);
        sceneCommand.AddOption(speedOption);
        sceneCommand.AddOption(brightnessOption);

        /*sceneIdArgument.AddValidator(parseArgument: result =>
        {
            if (result.GetValueForArgument(sceneIdArgument) == Scenes.Ocean) Console.WriteLine("cool");
        });
        */

        // Color temp command [API functionally allows a range of 1000-12000, but in actuallity caps at 6500]
        var whiteCommand = new Command("white", "Change the bulb to white");
        var tempArgument = new Argument<UInt16>("temp", "Color temperature in Kelvin (1000-6500)");

        rootCommand.AddCommand(whiteCommand);
        whiteCommand.AddArgument(tempArgument);
        whiteCommand.AddOption(brightnessOption);

        // Brightness commands
        var brightnessCommand = new Command("brightness", "Change the brightness of the bulb");
        var brightActionArgument = new Argument<BrightnessActions>("action", "Brightness action to perform");
        var brightValueArgument = new Argument<ushort>("amount", "Value of brightness or amount to increase or decrease brightness");

        rootCommand.AddCommand(brightnessCommand);
        brightnessCommand.AddArgument(brightActionArgument);
        brightnessCommand.AddArgument(brightValueArgument);

        // Power on/off
        var powerCommand = new Command("power", "Change the power state");
        var powerArgument = new Argument<PowerState>("state");

        rootCommand.AddCommand(powerCommand);
        powerCommand.AddArgument(powerArgument);

        // Discover Command
        var discoverCommand = new Command("discover", "Discover Wiz Lights on the network");
        rootCommand.Add(discoverCommand);

        // Make the default help prettier by formatting the scene enumeration
        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHelp(ctx =>
            {
                // Organize scene descriptions into 3, 15 character wide columns

                string formattedScenes = "";
                int column = 0;

                foreach (Scenes scene in Enum.GetValues(typeof(Scenes)))
                {
                    formattedScenes += String.Format("{0,-15}", scene.ToString().ToLower());
                    if (column == 2) formattedScenes += "\r\n";
                    column = (column + 1) % 3;
                }

                // Help text for root command menu (i.e. app.exe --help)
                ctx.HelpBuilder.CustomizeSymbol(sceneCommand,
                    firstColumnText: "scene <name>");

                // Help text for 
                ctx.HelpBuilder.CustomizeSymbol(sceneIdArgument,
                    firstColumnText: "<name>",
                    secondColumnText: "Scene name:\r\n" + formattedScenes);
            })
            .Build();


        rootCommand.SetHandler((ip) =>
        {
            Console.WriteLine("Never Get Here {0}", ip.GetType());
            return null;
        }, addressArgument);

        /*
        colorCommand.SetHandler((addressValue, port, red, green, blue) =>
        {
            Console.WriteLine("RGB {0}, {1}, {2}, {3}, {4}", addressValue, port, red, green, blue);
        }, addressArgument, portNumberOption, redArgument, greenArgument, blueArgument);
        */
        colorCommand.SetHandler(
            (address, port, red, green, blue, warm, cold, brightness) =>
            {
                RGBHandler(address.ToString(), port, red, green, blue, warm, cold, brightness);
            }, addressArgument, portNumberOption, redArgument, greenArgument, blueArgument, warmArgument, coldArgument, brightnessOption);
        whiteCommand.SetHandler(
            (address, port, temp, brightness) =>
            {
                WhiteHandler(address.ToString(), port, temp, brightness);
            }, addressArgument, portNumberOption, tempArgument, brightnessOption);

        sceneCommand.SetHandler(
            (address, port, sceneid, speed, brightness) =>
            {
                SceneHandler(address.ToString(), port, sceneid, speed, brightness);
            }, addressArgument, portNumberOption, sceneIdArgument, speedOption, brightnessOption);

        brightnessCommand.SetHandler(
            (address, port, action, amount) =>
            {
                BrightnessHandler(address.ToString(), port, action, amount);
            }, addressArgument, portNumberOption, brightActionArgument, brightValueArgument);

        powerCommand.SetHandler(
            (address, port, powerState, brightness) =>
            {
                PowerHandler(address.ToString(), port, powerState, brightness);
            }, addressArgument, portNumberOption, powerArgument, brightnessOption);




        parser.Invoke(args);

        return;
    }

    private static void RGBHandler(string address, ushort? port, ushort red, ushort green, ushort blue, ushort cold, ushort warm, ushort? brightness)
    {
        Console.WriteLine("RGB {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}", address, port, red, green, blue, cold, warm, brightness);

        IPAddress ipaddr = (Dns.GetHostAddresses(address)).FirstOrDefault();
        WizBulb wizBulb = new WizBulb(ipaddr);

        wizBulb.SetRGB(red, green, blue, cold, warm, brightness);
    }

    private static void WhiteHandler(string address, ushort? port, UInt16 temp, ushort? brightness)
    {
        Console.WriteLine("Scene {0}, {1}, {2}, {3}", address, port, temp, brightness);

        IPAddress ipaddr = (Dns.GetHostAddresses(address)).FirstOrDefault();
        WizBulb wizBulb = new WizBulb(ipaddr);

        wizBulb.SetWhite(temp, brightness);
    }

    private static void SceneHandler(string address, ushort? port, Scenes sceneId, ushort? speed, ushort? brightness)
    {
        Console.WriteLine("White {0}, {1}, {2}, {3}, {4}", address, port, sceneId, speed, brightness);

        IPAddress ipaddr = (Dns.GetHostAddresses(address)).FirstOrDefault();
        WizBulb wizBulb = new WizBulb(ipaddr);

        wizBulb.SetScene(sceneId, speed, brightness);
    }


    private static void BrightnessHandler(string address, ushort? port, BrightnessActions action, ushort amount)
    {
        ushort bright = 0;
        Console.WriteLine("Brightness {0}, {1}, {2}", address, action, amount);

        IPAddress test = (Dns.GetHostAddresses(address)).FirstOrDefault();
        WizBulb wizBulb = new WizBulb(test);

        ushort currentb = wizBulb.GetBrightness();
        Console.WriteLine("Current Brightness:  {0}", currentb);

        if (action == BrightnessActions.set)
            bright = amount;
        else if (action == BrightnessActions.plus)
            bright = (ushort)(currentb + amount);
        else if (action == BrightnessActions.minus)
            bright = (ushort)(currentb - amount);

        if (bright < 10) bright = 10;
        if (bright > 100) bright = 100;

        wizBulb.SetBrightness((ushort)bright);
        return;
    }

    private static void PowerHandler(string address, ushort? port, PowerState powerState, ushort? brightness)
    {
        Console.WriteLine("Power {0}, {1}, {2}, {3}", address, port, powerState, brightness);

        IPAddress test = (Dns.GetHostAddresses(address)).FirstOrDefault();
        WizBulb wizBulb = new WizBulb(test);

        if (powerState == PowerState.on)
            wizBulb.SetPower(true);
        else if (powerState == PowerState.off)
            wizBulb.SetPower(false);
        else if (powerState == PowerState.toggle)
            wizBulb.SetPower(!wizBulb.GetPower());

        if (brightness != null)
            wizBulb.SetBrightness((ushort)brightness);
        return;
    }
}