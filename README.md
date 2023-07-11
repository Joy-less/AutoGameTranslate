# AutoGameTranslate

Want to play a game, but it's in a language you don't understand?

AutoGameTranslate is a Windows tool which translates your games in real time. It regularly reads characters from the focused window using [Windows Media OCR](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine), translates them, and outputs them in a resizable overlay window.

To translate, you have the choice between:
- [Argos Translate](https://github.com/argosopentech/argos-translate) - Offline, slow, low quality (not recommended for now)
- [DeepL](https://www.deepl.com/pro-api) - Very high quality, 500k chars/month free
- [Azure](https://portal.azure.com/) - High quality, 2m chars/month free for 12 months

### Usage instructions:
- Open Settings, navigate to Language & region, and make sure that the original language is installed (for OCR purposes).
- Unzip `AutoGameTranslate.zip`.
- For Argos Translate:
  - Download your desired translation model from [here](https://www.argosopentech.com/argospm/index). Japanese to English is downloaded for you.
  - Place the model in the Translation folder, making sure to rename it to `xx-yy.argosmodel` where xx is the original language and yy is the target language (e.g. `ja-en.argosmodel`).
- Run `AutoGameTranslate.exe`.

### Disclaimer for APIs
Keep your API keys secret and only share them with trusted parties. Additionally, stay within your monthly quota if applicable. Please note that I cannot be held responsible if your API key is leaked or you exceed your quota limit.

### Example with DeepL

![Example with DeepL](DeepL%20Example.jpg)