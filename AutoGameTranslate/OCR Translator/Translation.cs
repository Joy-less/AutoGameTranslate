using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System;
using Newtonsoft.Json;
using System.Net;
using System.Windows.Input;

namespace OCR_Translator
{
    internal abstract class Translation
    {
        public Translation(string FromLanguage, string ToLanguage) {

        }
        public abstract Task<string> TranslateText(string Text);
        // public abstract List<string[]> GetAvailableTranslations();
    }
    internal class ArgosTranslation : Translation
    {
        const string TranslationRootDirectory = "Translation/";
        const string TranslationDirectory = TranslationRootDirectory + "translate/";
        const string ExeFile = TranslationDirectory + "translate.exe";
        const string InputFile = TranslationDirectory + "translate_input.txt";
        const string OutputFile = TranslationDirectory + "translate_output.txt";
        const string LanguageFile = TranslationDirectory + "language.txt";

        readonly Process TranslationProcess;

        public ArgosTranslation(string FromLanguage, string ToLanguage) : base(FromLanguage, ToLanguage) {
            File.WriteAllText(LanguageFile, FromLanguage + "\n" + ToLanguage);
            if (File.Exists(InputFile)) File.Delete(InputFile);
            TranslationProcess = Process.Start(ExeFile);
        }
        public void Kill() {
            TranslationProcess.Kill();
        }

        public static List<string[]> GetAvailableTranslations() {
            List<string[]> AvailableTranslations = new();
            foreach (string File in Directory.GetFiles(TranslationRootDirectory)) {
                if (Path.GetExtension(File) == ".argosmodel") {
                    string Translation = Path.GetFileNameWithoutExtension(File);
                    string[] Languages = Translation.Split('-');
                    if (Languages.Length == 2) {
                        AvailableTranslations.Add(new string[] { Languages[0], Languages[1] });
                    }
                }
            }
            return AvailableTranslations;
        }

        public override async Task<string> TranslateText(string Text) {
            await File.WriteAllTextAsync(InputFile, Text);
            while (true) {
                await Task.Delay(50);
                try {
                    if (File.Exists(OutputFile)) {
                        string Result = await File.ReadAllTextAsync(OutputFile);
                        File.Delete(OutputFile);
                        return Result;
                    }
                }
                catch {
                }
            }
        }

        public static void DeleteIntermediateFiles() {
            static void DeleteFile(string Path) {
                try {
                    if (File.Exists(Path)) File.Delete(Path);
                }
                catch {
                }
            }
            DeleteFile(InputFile);
            DeleteFile(OutputFile);
            DeleteFile(LanguageFile);
        }
    }
    internal class AzureTranslation : Translation
    {
        private const string ApiEndpoint = "https://api.cognitive.microsofttranslator.com";

        private static string Key = "";
        private static string Location = "";

        readonly string FromLanguage;
        readonly string ToLanguage;

        public AzureTranslation(string FromLanguage, string ToLanguage) : base(FromLanguage, ToLanguage) {
            this.FromLanguage = FromLanguage;
            this.ToLanguage = ToLanguage;
        }

        /*public override List<string[]> GetAvailableTranslations() {
            throw new NotImplementedException();
        }*/
        public override async Task<string> TranslateText(string Text) {
            string Route = "/translate?api-version=3.0&from=" + FromLanguage + "&to=" + ToLanguage;

            object[] Body = new object[] { new { Text = Text } };
            string RequestBody = JsonConvert.SerializeObject(Body);

            using (var Client = new HttpClient())
            using (var Request = new HttpRequestMessage()) {
                // Build the request.
                Request.Method = HttpMethod.Post;
                Request.RequestUri = new Uri(ApiEndpoint + Route);
                Request.Content = new StringContent(RequestBody, Encoding.UTF8, "application/json");
                Request.Headers.Add("Ocp-Apim-Subscription-Key", Key);
                Request.Headers.Add("Ocp-Apim-Subscription-Region", Location);

                // Send the request and get response.
                HttpResponseMessage Response = await Client.SendAsync(Request).ConfigureAwait(false);
                // Read response as a string.
                string Result = await Response.Content.ReadAsStringAsync();

                dynamic ResultObject = JsonConvert.DeserializeObject(Result) ?? false;
                try {
                    // e.g. [{"translations":[{"text":"translation 1","to":"sw"},{"text":"translation 2","to":"it"}]}]

                    return ResultObject[0]["translations"][0]["text"];
                }
                catch {
                    Environment.Exit(0);
                    return null;
                }
            }
        }

        public static void SetApiInfo(string Key, string Location) {
            AzureTranslation.Key = Key;
            AzureTranslation.Location = Location;
        }
    }
    internal class DeepLTranslation : Translation
    {
        readonly DeepL.Translator Translator;

        private static string Key = "";

        readonly string? FromLanguage;
        readonly string ToLanguage;

        public DeepLTranslation(string FromLanguage, string ToLanguage) : base(FromLanguage, ToLanguage) {
            if (string.IsNullOrWhiteSpace(FromLanguage)) this.FromLanguage = null;
            else this.FromLanguage = FromLanguage;
            this.ToLanguage = ToLanguage;

            try {
                Translator = new(Key);
            }
            catch {
                Environment.Exit(0);
            }
        }

        public override async Task<string> TranslateText(string Text) {
            try {
                DeepL.Model.TextResult Result = await Translator.TranslateTextAsync(Text, FromLanguage, ToLanguage);
                string ResultText = Result.Text;
                return ResultText;
            }
            catch (Exception ex) {
                File.WriteAllText("error.txt", ex.Message);
                Environment.Exit(0);
                return null;
            }
        }

        public static void SetApiInfo(string Key) {
            DeepLTranslation.Key = Key;
        }
    }
}
