using RichHudFramework.Internal;
using RichHudFramework.UI;
using RichHudFramework.UI.Server;
using RichHudFramework;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRageMath;

namespace RichHudFramework.Server
{
    using UI.Rendering.Server;

    public sealed partial class RichHudMaster
    {
        private readonly RichText loremIpsum = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam aliquet diam id lacus tristique semper. Morbi ut bibendum orci. Suspendisse potenti. Praesent vulputate sapien ac lacinia elementum. Nullam in sagittis augue. Proin vel commodo lacus. Donec imperdiet at velit non porttitor. Pellentesque ac purus libero. Phasellus id ex in augue placerat suscipit. Sed bibendum leo a nibh auctor, quis tincidunt purus ultrices. Nam facilisis elit nulla, non tincidunt metus sagittis in. Proin commodo neque tellus, sit amet congue augue iaculis ac.

In et leo non justo faucibus dignissim. Ut mi sem, ullamcorper et erat ut, auctor laoreet nisi. Donec in tristique erat. Duis cursus ultricies justo, eget lacinia tellus dictum a. Aenean orci neque, auctor at dignissim blandit, semper a quam. Aliquam erat volutpat. Nunc lobortis in arcu nec vehicula. Donec ac urna sed diam porttitor ultricies sit amet eu odio. Fusce eu suscipit turpis.

Proin eu urna velit. Donec a nulla gravida, blandit dolor eu, pulvinar ex. Etiam sit amet nunc nec neque feugiat suscipit. Donec venenatis tellus sagittis enim mollis dictum. Phasellus consectetur ut felis ac scelerisque. Fusce ullamcorper augue vitae sagittis efficitur. Maecenas ultricies neque nunc, ut dictum quam volutpat eget. Duis ut malesuada nisi. Sed tincidunt semper rhoncus. Nunc cursus laoreet luctus. Nullam ultricies libero quis ullamcorper porta. Donec a mattis lectus, sed tempor urna. Vestibulum efficitur, risus ac egestas malesuada, mauris neque eleifend quam, nec varius enim leo non ante. In lobortis tellus sed mauris malesuada, eget vulputate nibh gravida. Vivamus vehicula sem vel mi fringilla tincidunt.

Maecenas laoreet pellentesque purus, quis fringilla ligula. Phasellus consectetur dui pharetra ornare ornare. Aliquam non ligula lobortis, efficitur ipsum a, sodales lacus. Fusce facilisis magna id enim ultricies, vel efficitur odio porttitor. Ut nec tincidunt lorem, iaculis mollis nunc. Ut eget elit varius, porttitor odio in, pulvinar ligula. Donec at lorem ante. Curabitur placerat, ante et venenatis ultricies, eros mauris bibendum leo, vulputate auctor odio diam et leo. Nulla facilisi. Vestibulum sed mauris bibendum nisi dapibus blandit et sit amet justo. Vestibulum ullamcorper purus eu ultrices ultricies. Fusce risus quam, rhoncus vitae tempus id, fringilla sit amet mi.

Nunc dictum, arcu vel feugiat pellentesque, justo mi imperdiet mi, sed pellentesque purus purus vitae mi. Praesent fringilla accumsan nisi, sed dignissim nunc egestas hendrerit. Fusce egestas fermentum nisi a eleifend. Quisque dapibus neque at elit efficitur consectetur. Maecenas placerat finibus arcu, tincidunt sagittis massa tristique non. Aliquam tincidunt, nisl nec tempor imperdiet, felis ex congue magna, et tempor mauris eros id dui. Vestibulum tincidunt aliquam pellentesque. Suspendisse potenti. In in consectetur tellus. In mollis gravida egestas. Nullam scelerisque elit volutpat, aliquam erat sed, consequat elit. Proin interdum malesuada nibh ac lacinia. Integer eget condimentum dolor.

Donec vitae faucibus mi. Nam lacus nibh, pellentesque eget luctus vel, tristique sed lacus. Morbi bibendum lectus est. Sed ullamcorper vestibulum sollicitudin. Nunc ultrices facilisis porta. Nullam aliquam ante ut mi auctor sodales. Suspendisse ut tincidunt turpis, non posuere nunc. Sed rutrum varius dui, eget ullamcorper urna finibus non. Nullam a lobortis nunc, quis gravida dui. Nullam volutpat enim id lacinia feugiat. In dui lacus, viverra viverra vestibulum vitae, maximus eget nunc.

Aenean egestas nisl nisi, vitae faucibus justo efficitur mollis. Aliquam erat volutpat. Suspendisse volutpat, justo ac condimentum consequat, dui velit gravida elit, et facilisis augue eros quis tellus. In nisl dui, pulvinar id eleifend eget, aliquet non felis. Integer quis dictum nulla. Vivamus hendrerit risus id lorem congue ultricies non et magna. Proin justo leo, dictum at eros id, pharetra sagittis mi. Ut condimentum ex id porta maximus. Mauris ultricies purus est, vel malesuada tellus maximus eu. Mauris faucibus ornare mauris, ac mollis lorem sagittis ac. Vivamus tempor pharetra mi dictum faucibus. Vestibulum diam quam, tincidunt ac massa a, auctor sagittis diam. Sed sodales risus sit amet enim pellentesque, vel pharetra dui mattis. Ut lorem tortor, pulvinar at semper rutrum, tincidunt eget tellus.

Nam nec nunc eget elit porta fermentum id vel nisl. Vestibulum fermentum porta ipsum, non scelerisque ex suscipit eu. Aliquam neque urna, ornare at orci porttitor, consectetur euismod mauris. Nulla aliquam ex non faucibus tincidunt. Aenean at pharetra neque. Vestibulum lacinia suscipit mauris in ultricies. Proin finibus ipsum quis lobortis fermentum. Vivamus varius et ante in posuere.

Phasellus sit amet tellus pretium, dapibus mauris eget, lobortis leo. Nullam vitae odio ac risus tincidunt facilisis id in nunc. Ut et urna sit amet ligula varius congue at quis nunc. Nulla dapibus nulla sit amet felis volutpat rutrum. Phasellus porttitor, risus et condimentum vehicula, nunc ex pulvinar ex, vel vestibulum ex purus nec enim. Praesent euismod id enim id dignissim. Pellentesque vitae dolor vel elit hendrerit semper non id felis. Proin arcu orci, placerat at mi eget, egestas aliquam odio. Duis sollicitudin ligula viverra est vestibulum consectetur.

Pellentesque egestas, lacus vitae vehicula condimentum, nibh ipsum semper lacus, porta finibus magna purus et turpis. Quisque eget felis sit amet justo fringilla aliquet id a lacus. Fusce dapibus felis et arcu hendrerit ultrices. Sed vel velit id lorem lobortis finibus vel non ipsum. Aliquam efficitur nulla non orci tempor, nec sodales lacus facilisis. Nulla blandit luctus sodales. Morbi egestas eros et auctor venenatis. Morbi elementum turpis et diam maximus, sit amet sollicitudin ante convallis. Praesent dapibus lorem vitae lacus vestibulum, ac rutrum nunc posuere.

Praesent eros est, blandit et ullamcorper nec, tempus a dui. Duis arcu arcu, dictum sed cursus a, tincidunt id elit. Nunc sit amet ante in massa vulputate congue. Aenean eget diam est. Sed dignissim pellentesque risus, nec interdum nisl volutpat in. Mauris vel fringilla justo. Phasellus eget convallis massa. Praesent ornare tellus est, vel efficitur metus bibendum in. Pellentesque finibus massa eget velit tempor semper. Nulla maximus neque sed neque porttitor aliquet. Pellentesque a diam a lectus lacinia facilisis viverra et sapien. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Nulla in ligula ut lacus commodo varius. Etiam nec semper mauris. Integer facilisis facilisis massa, eget ullamcorper odio facilisis sit amet. Suspendisse potenti. Integer gravida ex nunc, et fringilla ante placerat vitae. Nulla urna tortor.";

        private TextBoard textBoard;

        private CmdGroupInitializer GetChatCommands()
        {
            return new CmdGroupInitializer
            {
                { "resetBinds", x => MasterBinds.Cfg = BindsConfig.Defaults },
                { "save", x => MasterConfig.SaveStart() },
                { "load", x => MasterConfig.LoadStart() },
                { "resetConfig", x => MasterConfig.ResetConfig()},
                { "open", x => RichHudTerminal.OpenMenu()},
                { "close", x => RichHudTerminal.CloseMenu() },
                { "toggleCursor", x => HudMain.EnableCursor = !HudMain.EnableCursor },
                { "crash", x => ThrowException()},
                { "toggleDebug", x => RichHudDebug.EnableDebug = !RichHudDebug.EnableDebug },
                { "textBench", TextBench, 2 },
                { "printConIDs", x => ExceptionHandler.WriteToLog(StringListToString(BindManager.SeControlIDs)) },
                { "printMouseConIDs", x => ExceptionHandler.WriteToLog(StringListToString(BindManager.SeMouseControlIDs)) },
                { "debugLogging", SetDebugLogging, 1 }
            };
        }

        private static void ThrowException()
        {
            throw new Exception("Crash chat command was called");
        }

        private void SetDebugLogging(string[] args)
        {
            bool isEnabled;

            if (bool.TryParse(args[0], out isEnabled))
            {
                ExceptionHandler.DebugLogging = isEnabled;
            }
        }

        private void TextBench(string[] args)
        {
            int iterations = 1;
            bool benchAssign, benchDraw;

            bool.TryParse(args[0], out benchDraw);
            bool.TryParse(args[1], out benchAssign);

            if (args.Length > 2)
                int.TryParse(args[2], out iterations);

            if (textBoard == null)
            {
                textBoard = new TextBoard() { AutoResize = true, BuilderMode = TextBuilderModes.Wrapped };
                textBoard.SetText(loremIpsum);
            }

            int charCount = 0;

            for (int i = 0; i < textBoard.Count; i++)
                charCount += textBoard[i].Count;

            var timer = new Stopwatch();
            timer.Start();

            if (benchAssign)
            {
                for (int i = 0; i < iterations; i++)
                    textBoard.SetText(loremIpsum);
            }

            if (benchDraw)
            {
                for (int i = 0; i < iterations; i++)
                    textBoard.Draw(Vector2.Zero);
            }

            timer.Stop();

            ExceptionHandler.SendChatMessage
            (
                $"Text Bench:\n" +
                $"\tBenchDraw: {benchDraw}\n" +
                $"\tBenchAssign: {benchAssign}\n" +
                $"\tCharCount: {charCount}\n" +
                $"\tTime: {(timer.ElapsedTicks / (double)TimeSpan.TicksPerMillisecond):G6} ms\n" +
                $"\tIsHighResolution: {Stopwatch.IsHighResolution}\n" +
                $"\tIterations: {iterations}"
            );
        }

        private static string StringListToString(IReadOnlyList<string> strings)
        {
            StringBuilder listBuilder = new StringBuilder();
            int len = strings.Count;

            for (int i = 0; i < strings.Count; i++)
                len += strings[i].Length;

            listBuilder.EnsureCapacity(len);

            for (int i = 0; i < strings.Count; i++)
                listBuilder.AppendLine(strings[i]);

            return listBuilder.ToString();
        }
    }
}