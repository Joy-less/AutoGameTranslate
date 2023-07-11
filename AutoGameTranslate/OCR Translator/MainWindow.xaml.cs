using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Globalization;

namespace OCR_Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static Ocr OCR;
        static Translation Translation;

        const string NormalTitle = "AutoGameTranslate";
        const string WorkingTitle = "AutoGameTranslate...";

        public MainWindow()
        {
            InitializeComponent();

            // Config
            Output.Text = string.Empty;
            Topmost = true;
            Title = NormalTitle;

            ConsoleHelper.AllocConsole();

            // Choose a translation
            Setup();

            OCR = new(SetupResult.FromLanguage);
            Task.Run(() => Start(SetupResult.TranslationServiceClasses[SetupResult.Service]));
        }
        static void RunInMainThread(Action Action) {
            try {
                Application.Current.Dispatcher.Invoke(Action);
            }
            catch {
            }
        }
        void OutputText(string Text) {
            RunInMainThread(() => {
                Output.Text = Text;
            });
        }

        async void Start(Type TranslationType) {
            ConsoleHelper.FreeConsole();

            Translation = (Translation)(Activator.CreateInstance(TranslationType, new object[] {SetupResult.FromLanguage, SetupResult.ToLanguage})
                ?? throw new Exception("Could not create translation class instance")); // new Translation(FromLanguage, ToLanguage);
            string LastOcrText = "";

            while (true) {
                try {
                    Bitmap? CapturedBitmap = null;
                    bool FailedToCaptureBitmap = false;
                    RunInMainThread(() => {
                        try {
                            Bitmap Bitmap = ScreenCapture.CaptureActiveWindow(out IntPtr ActiveWindowHandle);
                            Rectangle ThisWindowRectangle = ScreenCapture.GetWindowRectangle(new WindowInteropHelper(this).Handle);

                            Rectangle WindowRectangle = ScreenCapture.GetWindowRectangle(ActiveWindowHandle);

                            Rectangle LocalWindowRectangle = ThisWindowRectangle;
                            LocalWindowRectangle.Offset(new System.Drawing.Point(-WindowRectangle.Location.X, -WindowRectangle.Location.Y));

                            using (Graphics G = Graphics.FromImage(Bitmap)) {
                                G.FillRectangle(Brushes.White, LocalWindowRectangle);
                                if (SetupResult.IgnoreTitleBar) {
                                    G.FillRectangle(Brushes.White, new Rectangle(0, 0, Bitmap.Width, ScreenCapture.GetTitleBarHeight(ActiveWindowHandle)));
                                }
                            }

                            CapturedBitmap = Bitmap;
                        }
                        catch {
                            FailedToCaptureBitmap = true;
                        }
                    });

                    while (CapturedBitmap == null) {
                        await Task.Delay(10);
                        if (FailedToCaptureBitmap == true) {
                            FailedToCaptureBitmap = false;
                            throw new Exception("Failed to capture bitmap");
                        }
                    }

                    OcrResult Result = OCR.GetTextFromBitmap(CapturedBitmap, ImageFormat.Jpeg).Result;
                    if (LastOcrText != Result.Text) {
                        RunInMainThread(() => Title = WorkingTitle);
                        LastOcrText = Result.Text;

                        await OutputResult(Result);

                        RunInMainThread(() => Title = NormalTitle);
                    }
                }
                catch {
                }
                Thread.Sleep(200);
            }
        }
        async Task OutputResult(OcrResult? Result) {
            if (Result == null) return;

            System.Text.StringBuilder Text = new();
            foreach (OcrLine Line in Result.Lines) {
                Text.AppendLine(Line.Text);
            }
            string UntranslatedResult = Text.ToString().Trim().Truncate(SetupResult.MaxCharas);
            string TranslatedResult = await Translation.TranslateText(UntranslatedResult);

            if (SetupResult.DisplayOriginalTextBesideTranslation) {
                OutputText("ORIGINAL:\n" + UntranslatedResult + "\nTRANSLATED:\n" + TranslatedResult);
            }
            else {
                OutputText(TranslatedResult);
            }
        }
        static class SetupResult {
            public static TranslationService Service;

            public static string FromLanguage;
            public static string ToLanguage;
            public static int MaxCharas;
            public static bool DisplayOriginalTextBesideTranslation = false;
            public static bool IgnoreTitleBar = true;

            public enum TranslationService {
                ArgosTranslate,
                DeepL,
                Azure,
            }
            public static Dictionary<TranslationService, Type> TranslationServiceClasses = new() {
                {TranslationService.ArgosTranslate, typeof(ArgosTranslation)},
                {TranslationService.DeepL, typeof(DeepLTranslation)},
                {TranslationService.Azure, typeof(AzureTranslation)},
            };
            public static Dictionary<TranslationService, string> TranslationServiceDisplayNames = new() {
                {TranslationService.ArgosTranslate, "Argos Translate (Offline)"},
                {TranslationService.DeepL, "DeepL"},
                {TranslationService.Azure, "Azure"},
            };
        }
        static void Setup() {
            static void ClearInput() {
                while (Console.KeyAvailable) {
                    Console.ReadKey(true);
                }
            }
            static int ChooseFromOptions(string Title, dynamic Options) {
                ClearInput();
                Console.WriteLine(Title);

                for (int i = 0; i < Options.Length; i++) {
                    Console.WriteLine(i + 1 + ". " + Options[i]);
                }
                int Input;
                while (true) {
                    string InputText = Console.ReadLine() ?? "";
                    if (int.TryParse(InputText, out Input) && Input >= 1 && Input <= Options.Length) {
                        break;
                    }
                    else {
                        Console.WriteLine("Invalid input.");
                    }
                }
                return Input - 1;
            }
            static bool ChooseFromYesNo(string Title) {
                return ChooseFromOptions(Title, new[] {"Yes", "No"}) == 0;
            }
            static int InputInt(string Title, int? Default = null, int? Minimum = null) {
                ClearInput();
                Console.Write(Title);
                if (Default != null) Console.Write(" (Leave blank to default to " + Default + ")");
                Console.WriteLine();
                while (true) {
                    string InputText = Console.ReadLine() ?? "";
                    if (int.TryParse(InputText, out int InputTextAsInt) && InputTextAsInt >= (Minimum ?? int.MinValue)) {
                        return InputTextAsInt;
                    }
                    else if (Default != null && string.IsNullOrWhiteSpace(InputText)) {
                        return Default.Value;
                    }
                    else {
                        Console.WriteLine("Invalid input.");
                    }
                }
            }
            static string InputString(string Title, string? Default = null) {
                ClearInput();
                Console.Write(Title);
                if (Default != null) Console.Write(" (Leave blank to default to " + Default + ")");
                Console.WriteLine();
                while (true) {
                    string InputText = Console.ReadLine() ?? "";
                    if (Default != null && string.IsNullOrWhiteSpace(InputText)) {
                        return Default;
                    }
                    else {
                        return InputText;
                    }
                }
            }

            // Choose translation service
            SetupResult.TranslationService[] Services = (SetupResult.TranslationService[])Enum.GetValues(typeof(SetupResult.TranslationService));
            string[] ServiceDisplayNames = new string[Services.Length];
            for (int i = 0; i < Services.Length; i++) {
                ServiceDisplayNames[i] = SetupResult.TranslationServiceDisplayNames[Services[i]];
            }
            SetupResult.Service = Services[ChooseFromOptions("Which translation service would you like?", ServiceDisplayNames)];

            // Argos: choose translation model
            if (SetupResult.Service == SetupResult.TranslationService.ArgosTranslate) {
                List<string[]> AvailableModels = ArgosTranslation.GetAvailableTranslations();
                if (AvailableModels.Count == 0) {
                    Console.Write("No translation models available. Press enter to exit.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
                else {
                    string[] AvailableModelStrings = new string[AvailableModels.Count];
                    for (int i = 0; i < AvailableModels.Count; i++) {
                        AvailableModelStrings[i] = AvailableModels[i][0] + " -> " + AvailableModels[i][1];
                    }
                    string[] Model = AvailableModels[ChooseFromOptions("Choose a model:", AvailableModelStrings)];
                    SetupResult.FromLanguage = Model[0];
                    SetupResult.ToLanguage = Model[1];
                }
            }
            // Azure: Set API key
            else if (SetupResult.Service == SetupResult.TranslationService.Azure) {
                string Key = InputString("Enter Azure translation API key:");
                string Location = InputString("Enter Azure translation API location/region:");

                AzureTranslation.SetApiInfo(Key, Location);
            }
            // DeepL: Set API key
            else if (SetupResult.Service == SetupResult.TranslationService.DeepL) {
                string Key = InputString("Enter DeepL translation API key:");

                DeepLTranslation.SetApiInfo(Key);
            }

            // Set from and to language
            if (SetupResult.Service != SetupResult.TranslationService.ArgosTranslate) {
                SetupResult.FromLanguage = InputString("What is the language code (e.g. 'ja') of the original language?").Trim().ToLower();
                SetupResult.ToLanguage = InputString("What is the language code (e.g. 'en') of the target language?").Trim().ToLower();
            }

            // DeepL fix
            if (SetupResult.Service == SetupResult.TranslationService.DeepL) {
                Dictionary<string, string[]> LanguageVariants = new() {
                    {"en", new[] {"en-GB", "en-US"}},
                    {"pt", new[] {"pt-BR", "pt-PT"}},
                };
                if (LanguageVariants.TryGetValue(SetupResult.ToLanguage, out string[]? Variants)) {
                    int Variant = ChooseFromOptions("As you are using '" + SetupResult.ToLanguage + "' as a DeepL target language, please specify which variant:", Variants);
                    SetupResult.ToLanguage = Variants[Variant];
                }
            }

            // Set max characters
            SetupResult.MaxCharas = InputInt("Maximum characters to translate at once?", 200, 1);

            // Advanced options
            if (ChooseFromYesNo("View advanced setup options?")) {
                // Set display original text beside translation
                SetupResult.DisplayOriginalTextBesideTranslation = ChooseFromYesNo("Display the original text recognised by OCR beside the translation (for debug purposes?)");

                // Set ignore title bar
                SetupResult.IgnoreTitleBar = ChooseFromYesNo("Translate the title bar? (Not Recommended)");
            }
        }

        static class ConsoleHelper {
            [DllImport("Kernel32")]
            public static extern void AllocConsole();

            [DllImport("Kernel32", SetLastError = true)]
            public static extern void FreeConsole();
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (Translation is ArgosTranslation ArgosTranslation) {
                ArgosTranslation.Kill();
                ArgosTranslation.DeleteIntermediateFiles();
            }
        }
    }
    internal class Ocr
    {
        readonly OcrEngine Engine;

        public Ocr(string LanguageTag) {
            // Create OCR engine
            Engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language(LanguageTag));
            if (Engine == null) {
                Console.WriteLine("Failed to create OCR engine from language '" + LanguageTag + "'.");
                Console.WriteLine("Available languages:");
                foreach (Windows.Globalization.Language Language in OcrEngine.AvailableRecognizerLanguages) {
                    Console.WriteLine("- " + Language.DisplayName);
                }
                Console.WriteLine("Please install the language in settings.");
                Console.Write("Press enter to exit.");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        public async Task<OcrResult> GetTextFromBitmap(Bitmap Bitmap, ImageFormat Format) {
            SoftwareBitmap SoftwareBitmap = await GetSoftwareBitmapFromBitmap(Bitmap, Format);
            OcrResult Result = await Engine.RecognizeAsync(SoftwareBitmap);
            return Result;
        }
        static async Task<SoftwareBitmap> GetSoftwareBitmapFromBitmap(Bitmap Bitmap, ImageFormat Format) {
            using (InMemoryRandomAccessStream Stream = new()) {
                Bitmap.Save(Stream.AsStream(), Format); // Choose the specific image format by your own bitmap source
                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(Stream);
                SoftwareBitmap SoftwareBitmap = await Decoder.GetSoftwareBitmapAsync();
                return SoftwareBitmap;
            }
        }
    }
    internal static class StringExt
    {
        public static string Truncate(this string Value, int MaxLength) {
            if (string.IsNullOrEmpty(Value)) return Value;
            return Value.Length <= MaxLength ? Value : string.Concat(Value.AsSpan(0, MaxLength), "...");
        }
    }

    internal class ScreenCapture
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetDesktopWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public Rectangle ToRectangle() {
                return new Rectangle(Left, Top, Right - Left, Bottom - Top);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
        [DllImport("user32.dll")]
        private static extern IntPtr GetClientRect(IntPtr hWnd, ref Rect rect);
        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        public static Image CaptureDesktop() {
            return CaptureWindow(GetDesktopWindow());
        }

        public static Bitmap CaptureActiveWindow() {
            return CaptureWindow(GetForegroundWindow());
        }

        public static Bitmap CaptureWindow(IntPtr handle) {
            Rectangle bounds = GetWindowRectangle(handle);
            var result = new Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Graphics.FromImage(result)) {
                graphics.CopyFromScreen(new System.Drawing.Point(bounds.Left, bounds.Top), System.Drawing.Point.Empty, bounds.Size);
            }

            return result;
        }

        /*public static Rectangle GetWindowRectangle(IntPtr Handle) {
            Rect Rect = new();
            GetWindowRect(Handle, ref Rect);
            Rect LocalRect = new();
            GetClientRect(Handle, ref LocalRect);
            LocalRect.Left = Rect.Left + LocalRect.Left;
            LocalRect.Top = Rect.Top + LocalRect.Top;
            LocalRect.Right = Rect.Right + LocalRect.Right;
            LocalRect.Bottom = Rect.Bottom + LocalRect.Bottom;
            return LocalRect.ToRectangle();
        }*/

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

        [Flags]
        enum DwmWindowAttribute : uint {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY,
            DWMWA_TRANSITIONS_FORCEDISABLED,
            DWMWA_ALLOW_NCPAINT,
            DWMWA_CAPTION_BUTTON_BOUNDS,
            DWMWA_NONCLIENT_RTL_LAYOUT,
            DWMWA_FORCE_ICONIC_REPRESENTATION,
            DWMWA_FLIP3D_POLICY,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            DWMWA_HAS_ICONIC_BITMAP,
            DWMWA_DISALLOW_PEEK,
            DWMWA_EXCLUDED_FROM_PEEK,
            DWMWA_CLOAK,
            DWMWA_CLOAKED,
            DWMWA_FREEZE_REPRESENTATION,
            DWMWA_LAST
        }

        public static Rectangle GetWindowRectangle(IntPtr hWnd) {
            int size = Marshal.SizeOf(typeof(Rect));
            _ = DwmGetWindowAttribute(hWnd, (int)DwmWindowAttribute.DWMWA_EXTENDED_FRAME_BOUNDS, out Rect rect, size);
            return rect.ToRectangle();
        }

        public static int GetTitleBarHeight(IntPtr Handle) {
            Rect wrect = new();
            GetWindowRect(Handle, ref wrect);
            Rect crect = new();
            GetClientRect(Handle, ref crect);
            System.Drawing.Point lefttop = new System.Drawing.Point(crect.Left, crect.Top); // Practically both are 0
            ClientToScreen(Handle, ref lefttop);
            System.Drawing.Point rightbottom = new System.Drawing.Point(crect.Right, crect.Bottom);
            ClientToScreen(Handle, ref rightbottom);

            /* int left_border = lefttop.X - wrect.Left; // Windows 10: includes transparent part
            int right_border = wrect.Right - rightbottom.X; // As above
            int bottom_border = wrect.Bottom - rightbottom.Y; // As above */
            int top_border_with_title_bar = lefttop.Y - wrect.Top; // There is no transparent part

            return top_border_with_title_bar;
        }
        public static Bitmap CaptureActiveWindow(out IntPtr ActiveWindowHandle) {
            ActiveWindowHandle = GetForegroundWindow();
            return CaptureWindow(ActiveWindowHandle);
        }
    }
}
