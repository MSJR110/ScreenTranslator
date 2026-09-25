using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace ScreenTranslator.UI;

/// <summary>
/// The interface language. Everything the user reads lives here as a (Persian, English) pair; the app picks one at
/// startup from the setting ("auto" follows Windows) and keeps it for the session, so no live re-binding is needed.
/// XAML pulls strings with {ui:L key} and mirrors itself with FlowDirection="{ui:Flow}".
/// </summary>
public static class Loc
{
    private static bool _fa = true;

    public static bool IsFa => _fa;

    /// <summary>Reading direction of the interface.</summary>
    public static FlowDirection Flow => _fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>"auto" follows the Windows display language; "fa"/"en" pin it.</summary>
    public static void Use(string? setting)
    {
        _fa = setting switch
        {
            "fa" => true,
            "en" => false,
            _ => IsRtl(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
        };
    }

    /// <summary>Languages written right-to-left — used for the interface and for the translated text itself.</summary>
    public static bool IsRtl(string? languageTag)
    {
        if (string.IsNullOrEmpty(languageTag)) return false;
        var two = languageTag.Length > 2 ? languageTag[..2] : languageTag;
        return two.ToLowerInvariant() is "fa" or "ar" or "he" or "iw" or "ur" or "ps" or "yi" or "ku" or "sd";
    }

    public static FlowDirection FlowOf(string? languageTag)
        => IsRtl(languageTag) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>
    /// Reading direction of a piece of text, from its first strong character — history entries keep no language tag,
    /// and a Persian line laid out left-to-right puts its full stop on the wrong side.
    /// </summary>
    public static FlowDirection DetectFlow(string? text)
    {
        foreach (var c in text ?? "")
        {
            if (c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿')
                return FlowDirection.RightToLeft;
            if (char.IsLetter(c)) return FlowDirection.LeftToRight;
        }
        return Flow;
    }

    public static string T(string key)
        => Strings.TryGetValue(key, out var pair) ? (_fa ? pair.fa : pair.en) : key;

    public static string T(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Name of a language, in the interface language.</summary>
    public static string LanguageName(string? tag)
    {
        if (string.IsNullOrEmpty(tag) || tag == "auto") return T("common.auto");
        if (_fa && PersianLanguageNames.TryGetValue(tag, out var fa)) return fa;
        try { return CultureInfo.GetCultureInfo(tag).EnglishName; }
        catch { return tag.ToUpperInvariant(); }
    }

    private static readonly Dictionary<string, string> PersianLanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "انگلیسی", ["fa"] = "فارسی", ["ar"] = "عربی", ["tr"] = "ترکی", ["de"] = "آلمانی",
        ["fr"] = "فرانسوی", ["es"] = "اسپانیایی", ["it"] = "ایتالیایی", ["pt"] = "پرتغالی", ["ru"] = "روسی",
        ["zh-CN"] = "چینی", ["zh-TW"] = "چینی", ["zh"] = "چینی", ["ja"] = "ژاپنی", ["ko"] = "کره‌ای", ["hi"] = "هندی",
        ["ur"] = "اردو", ["nl"] = "هلندی", ["sv"] = "سوئدی", ["pl"] = "لهستانی", ["uk"] = "اوکراینی", ["id"] = "اندونزیایی",
    };

    private static readonly Dictionary<string, (string fa, string en)> Strings = new(StringComparer.Ordinal)
    {
        // ---- Shared ----------------------------------------------------
        ["common.close"] = ("بستن", "Close"),
        ["common.close.esc"] = ("بستن (Esc)", "Close (Esc)"),
        ["common.save"] = ("ذخیره", "Save"),
        ["common.saved"] = ("ذخیره شد ✓", "Saved ✓"),
        ["common.auto"] = ("خودکار", "Auto"),
        ["common.error"] = ("خطا", "Error"),
        ["common.translating"] = ("در حال ترجمه…", "Translating…"),
        ["common.cached"] = ("از کش", "cached"),
        ["common.copied"] = ("کپی شد ✓", "Copied ✓"),
        ["common.pin"] = ("سنجاق: با کلیک بیرون بسته نشود", "Pin: keep open when clicking away"),
        ["common.ok"] = ("باشه", "OK"),
        ["common.confirm"] = ("تأیید", "Confirm"),
        ["common.cancel"] = ("انصراف", "Cancel"),

        // ---- Toasts and errors ----------------------------------------
        ["toast.hotkey.title"] = ("میان‌بر در دسترس نیست", "Shortcut unavailable"),
        ["toast.hotkey.body"] = ("{0} توسط برنامه‌ی دیگری گرفته شده. از تنظیمات عوضش کن.",
                                 "{0} is already taken by another app. Pick another one in Settings."),
        ["toast.ready.title"] = ("ScreenTranslator آماده است", "ScreenTranslator is ready"),
        ["toast.ready.body"] = ("{0} ناحیه  ·  {1} کلمه  ·  {2} زنده", "{0} region  ·  {1} word  ·  {2} live"),
        ["toast.noword.title"] = ("کلمه‌ای زیر موس پیدا نشد", "No word under the cursor"),
        ["toast.noword.body"] = ("نشانگر را روی یک کلمه بگذار و {0} بزن.", "Point at a word and press {0}."),
        ["toast.notext.title"] = ("متنی در این ناحیه پیدا نشد", "No text found in that region"),
        ["toast.clipboard.title"] = ("کلیپ‌بورد در دسترس نبود", "The clipboard was busy"),
        ["toast.clipboard.body"] = ("برنامه‌ی دیگری آن را قفل کرده؛ دوباره امتحان کن.",
                                    "Another app is holding it — try again."),
        ["toast.copied.title"] = ("متن کپی شد", "Text copied"),
        ["toast.copied.body"] = ("{0} خط · {1} کلمه · OCR {2}ms", "{0} lines · {1} words · OCR {2}ms"),
        ["toast.livewindow.title"] = ("ترجمه‌ی زنده", "Live translation"),
        ["toast.livewindow.body"] = ("اول پنجره‌ای را که می‌خواهی ترجمه شود فعال کن، بعد {0} بزن.",
                                     "Focus the window you want translated first, then press {0}."),

        ["error.rate"] = ("سرویس ترجمه موقتاً محدود کرده. چند ثانیه صبر کن و دوباره امتحان کن.",
                          "The translation service is rate-limiting. Wait a few seconds and try again."),
        ["error.offline"] = ("اینترنت وصل نیست.", "No internet connection."),
        ["error.connect"] = ("اتصال به سرویس ترجمه برقرار نشد. اینترنت یا فیلترشکن را بررسی کن.",
                             "Could not reach the translation service. Check your connection or proxy."),
        ["error.timeout"] = ("سرویس ترجمه دیر جواب داد. دوباره امتحان کن.",
                             "The translation service timed out. Try again."),
        ["error.notext.region"] = ("متنی در این ناحیه پیدا نشد.", "No text found in that region."),
        ["error.noselection"] = ("متنی انتخاب نشده و کلیپ‌بورد هم خالی است.",
                                 "Nothing is selected and the clipboard is empty."),

        // ---- History modes (stored as keys, shown translated) -----------
        ["mode.region"] = ("ناحیه", "Region"),
        ["mode.selection"] = ("انتخاب", "Selection"),
        ["mode.clipboard"] = ("کلیپ‌بورد", "Clipboard"),

        // ---- Result popup ---------------------------------------------
        ["result.preparing"] = ("در حال آماده‌سازی…", "Getting ready…"),
        ["result.reading"] = ("در حال خواندن متن…", "Reading the text…"),
        ["result.selection"] = ("متن انتخاب‌شده", "Selected text"),
        ["result.fromclipboard"] = ("از کلیپ‌بورد", "From the clipboard"),
        ["result.langpair"] = ("{0} ← {1}", "{0} → {1}"),
        ["result.copy.translation"] = ("کپی ترجمه", "Copy translation"),
        ["result.copy.original"] = ("کپی متن اصلی", "Copy original"),
        ["result.speak"] = ("خواندن متن اصلی", "Read the original aloud"),

        // ---- Word card -------------------------------------------------
        ["word.speak"] = ("تلفظ", "Pronounce"),
        ["word.nodefs"] = ("تعریفی برای این کلمه پیدا نشد.", "No definition found for this word."),
        ["word.line"] = ("ترجمه‌ی کل خط", "Translate the line"),
        ["word.copy"] = ("کپی معنی", "Copy meaning"),

        // ---- Region selector -------------------------------------------
        ["select.live"] = ("ناحیه‌ای را که می‌خواهی زنده ترجمه شود بکش  ·  Esc برای لغو",
                           "Drag the region you want translated live  ·  Esc to cancel"),
        ["select.copy"] = ("ناحیه‌ای را بکش تا متنش کپی شود  ·  Esc برای لغو",
                           "Drag a region to copy its text  ·  Esc to cancel"),
        ["select.translate"] = ("ناحیه‌ی موردنظر را بکش  ·  Esc یا راست‌کلیک برای لغو",
                                "Drag the region you want  ·  Esc or right-click to cancel"),

        // ---- Live mode --------------------------------------------------
        ["live.title"] = ("ترجمه‌ی زنده", "Live translation"),
        ["live.stop.tip"] = ("پایان ترجمه‌ی زنده", "Stop live translation"),
        ["live.reselect"] = ("انتخاب دوباره‌ی ناحیه", "Pick the region again"),
        ["live.pause"] = ("مکث", "Pause"),
        ["live.resume"] = ("ادامه", "Resume"),
        ["live.state.error"] = ("خطا", "Error"),
        ["live.state.waiting"] = ("پنجره پیدا نشد", "Window not found"),
        ["live.waiting.text"] = ("در انتظار متن…", "Waiting for text…"),
        ["live.blocks"] = ("{0} بلوک", "{0} blocks"),
        ["live.translate.ms"] = ("ترجمه {0}ms", "translate {0}ms"),

        // ---- Tray flyout -------------------------------------------------
        ["tray.status.ready"] = ("آماده · در پس‌زمینه", "Ready · in the background"),
        ["tray.status.live"] = ("ترجمه‌ی زنده فعال", "Live translation running"),
        ["tray.exit"] = ("خروج از برنامه", "Quit ScreenTranslator"),
        ["tray.tile.region"] = ("ترجمه‌ی ناحیه", "Translate a region"),
        ["tray.tile.selection"] = ("متن انتخاب‌شده", "Selected text"),
        ["tray.tile.word"] = ("معنی کلمه‌ی زیر موس", "Define a word"),
        ["tray.tile.copy"] = ("کپی متن از تصویر", "Copy text (OCR)"),
        ["tray.live.hint"] = ("روی متن اصلی، همان‌جا", "right over the original"),
        ["tray.live.running"] = ("در حال اجرا", "running"),
        ["tray.live.region"] = ("ناحیه", "Region"),
        ["tray.live.window"] = ("پنجره‌ی فعال", "Active window"),
        ["tray.live.screen"] = ("کل صفحه", "Whole screen"),
        ["tray.live.stop"] = ("پایان ترجمه‌ی زنده", "Stop live translation"),
        ["tray.history"] = ("تاریخچه", "History"),
        ["tray.settings"] = ("تنظیمات", "Settings"),
        ["tray.help"] = ("راهنمای شروع", "Getting started"),
        ["tray.tooltip.live"] = ("ScreenTranslator — ترجمه‌ی زنده فعال", "ScreenTranslator — live translation running"),

        // ---- History window -----------------------------------------------
        ["history.title"] = ("تاریخچه‌ی ترجمه‌ها", "Translation history"),
        ["history.clear"] = ("پاک کردن همه", "Clear all"),
        ["history.search"] = ("جستجو در متن اصلی یا ترجمه…", "Search the original or the translation…"),
        ["history.empty"] = ("هنوز چیزی ترجمه نکرده‌ای", "Nothing translated yet"),
        ["history.confirm"] = ("همه‌ی تاریخچه پاک شود؟", "Clear the entire history?"),
        ["history.confirm.body"] = ("این کار برگشت‌پذیر نیست.", "This cannot be undone."),
        ["history.copy"] = ("کپی ترجمه", "Copy translation"),
        ["history.count"] = ("{0} مورد", "{0} entries"),
        ["time.now"] = ("همین الان", "just now"),
        ["time.minutes"] = ("{0} دقیقه پیش", "{0} min ago"),
        ["time.hours"] = ("{0} ساعت پیش", "{0} h ago"),
        ["time.days"] = ("{0} روز پیش", "{0} d ago"),

        // ---- Welcome -------------------------------------------------------
        ["welcome.title"] = ("خوش آمدی به ScreenTranslator", "Welcome to ScreenTranslator"),
        ["welcome.subtitle"] = ("هر متنی روی صفحه را بخوان و ترجمه کن — بدون کپی، بدون سوییچ پنجره. برنامه در کنار ساعت ویندوز می‌نشیند و فقط با میان‌بر بیدار می‌شود.",
                                "Read and translate anything on your screen — no copy-paste, no window switching. The app sits next to the Windows clock and wakes up on a shortcut."),
        ["welcome.region.title"] = ("ترجمه‌ی ناحیه", "Translate a region"),
        ["welcome.region.body"] = ("میان‌بر را بزن، دور متن بکش، ترجمه را کنارش ببین. برای عکس، ویدئو، بازی و PDF.",
                                   "Press the shortcut, drag around the text, read the translation next to it. Works on images, video, games and PDFs."),
        ["welcome.selection.title"] = ("ترجمه‌ی متن انتخاب‌شده", "Translate selected text"),
        ["welcome.selection.body"] = ("متن را در هر برنامه‌ای انتخاب کن و میان‌بر را بزن. بدون OCR، دقیق‌ترین حالت.",
                                      "Select text in any app and press the shortcut. No OCR involved — the most accurate mode."),
        ["welcome.live.title"] = ("ترجمه‌ی زنده", "Live translation"),
        ["welcome.live.body"] = ("یک ناحیه یا پنجره را انتخاب کن؛ ترجمه دقیقاً روی متن اصلی نقاشی می‌شود و با اسکرول به‌روز می‌ماند. Ctrl را نگه دار تا اصل را ببینی.",
                                 "Pick a region or a window: the translation is painted right over the original and keeps up as you scroll. Hold Ctrl to see the source."),
        ["welcome.word.title"] = ("معنی کلمه", "Word lookup"),
        ["welcome.word.body"] = ("موس را روی یک کلمه بگذار و میان‌بر را بزن: تلفظ، معنی فارسی و تعریف انگلیسی. برای کپی متن بدون ترجمه هم میان‌بر جدا دارد.",
                                 "Point at a word and press the shortcut: pronunciation, meaning and English definitions. Copying text without translating has its own shortcut too."),
        ["welcome.sample.label"] = ("نمونه — همین‌جا امتحان کن", "Sample — try it right here"),
        ["welcome.sample.button"] = ("ترجمه‌ی زنده روی همین پنجره", "Live-translate this window"),
        ["welcome.startup"] = ("اجرا هنگام روشن شدن ویندوز", "Start with Windows"),
        ["welcome.start"] = ("شروع کن", "Get started"),

        // ---- Settings -------------------------------------------------------
        ["settings.title"] = ("تنظیمات ScreenTranslator", "ScreenTranslator settings"),
        ["settings.tab.general"] = ("عمومی", "General"),
        ["settings.tab.hotkeys"] = ("میان‌برها", "Shortcuts"),
        ["settings.tab.live"] = ("ترجمه‌ی زنده", "Live"),
        ["settings.tab.engine"] = ("موتور ترجمه", "Engine"),
        ["settings.tab.about"] = ("درباره", "About"),

        ["settings.section.language"] = ("زبان", "Language"),
        ["settings.target"] = ("ترجمه به", "Translate into"),
        ["settings.uilang"] = ("زبان برنامه", "App language"),
        ["settings.uilang.auto"] = ("خودکار (زبان ویندوز)", "Automatic"),
        ["settings.uilang.hint"] = ("بعد از ذخیره، پنجره‌های جدید به زبان تازه باز می‌شوند.",
                                    "After saving, newly opened windows use the new language."),
        ["settings.ocr"] = ("زبان OCR (خواندن متن از تصویر)", "OCR language"),
        ["settings.ocr.hint"] = ("برای زبان‌های دیگر، از Settings › Time & language › Language بسته‌ی زبان را با گزینه‌ی OCR نصب کن.",
                                 "For other languages, install the language pack with its OCR option from Settings › Time & language › Language."),
        ["settings.ocr.auto"] = ("خودکار (زبان ویندوز)", "Automatic"),

        ["settings.section.look"] = ("ظاهر", "Appearance"),
        ["settings.theme"] = ("تم", "Theme"),
        ["settings.theme.system"] = ("همراه با ویندوز", "Follow Windows"),
        ["settings.theme.dark"] = ("تیره", "Dark"),
        ["settings.theme.light"] = ("روشن", "Light"),
        ["settings.popupfont"] = ("اندازه‌ی فونت ترجمه در پاپ‌آپ", "Translation font size in the popup"),

        ["settings.section.behavior"] = ("رفتار", "Behavior"),
        ["settings.startup"] = ("اجرا هنگام روشن شدن ویندوز", "Start with Windows"),
        ["settings.speak"] = ("دکمه‌ی خواندن متن اصلی در پاپ‌آپ", "Show the read-aloud button in the popup"),
        ["settings.history"] = ("ذخیره‌ی تاریخچه‌ی ترجمه‌ها (روی همین سیستم)", "Keep a translation history (on this machine)"),

        ["settings.section.hotkeys"] = ("میان‌برهای سراسری", "Global shortcuts"),
        ["settings.hotkeys.hint"] = ("روی کادر کلیک کن و ترکیب جدید را بزن. Backspace برای حذف. ترکیب باید Ctrl/Alt/Shift/Win یا یک کلید F داشته باشد.",
                                     "Click a box and press the new combination. Backspace clears it. It needs Ctrl/Alt/Shift/Win or an F-key."),
        ["settings.hk.region"] = ("ترجمه‌ی ناحیه", "Translate a region"),
        ["settings.hk.selection"] = ("ترجمه‌ی متن انتخاب‌شده", "Translate selected text"),
        ["settings.hk.liveregion"] = ("ترجمه‌ی زنده‌ی ناحیه (شروع/پایان)", "Live-translate a region (start/stop)"),
        ["settings.hk.livewindow"] = ("ترجمه‌ی زنده‌ی پنجره‌ی فعال", "Live-translate the active window"),
        ["settings.hk.word"] = ("معنی کلمه‌ی زیر موس", "Define the word under the cursor"),
        ["settings.hk.word.hint"] = ("تلفظ، معنی فارسی و تعریف انگلیسی", "Pronunciation, meaning and English definitions"),
        ["settings.hk.copy"] = ("کپی متن از تصویر", "Copy text from an image"),
        ["settings.hk.copy.hint"] = ("OCR بدون ترجمه؛ متن به کلیپ‌بورد می‌رود", "OCR without translating; the text goes to the clipboard"),

        ["settings.section.inapp"] = ("داخل برنامه", "Inside the app"),
        ["settings.inapp.peek"] = ("دیدن متن اصلی زیر ترجمه‌ی زنده", "Peek at the original under a live translation"),
        ["settings.inapp.peek.key"] = ("نگه‌داشتن Ctrl", "Hold Ctrl"),
        ["settings.inapp.close"] = ("بستن پاپ‌آپ / لغو انتخاب", "Close the popup / cancel a selection"),
        ["settings.inapp.copy"] = ("کپی ترجمه در پاپ‌آپ", "Copy the translation in the popup"),
        ["settings.inapp.resize"] = ("تغییر اندازه‌ی پاپ‌آپ / برگشت به اندازه‌ی خودکار", "Resize the popup / back to auto size"),
        ["settings.inapp.resize.key"] = ("کشیدن لبه / دابل‌کلیک سرتیتر", "Drag an edge / double-click the header"),
        ["settings.inapp.speak"] = ("تلفظ در کارت کلمه", "Pronounce in the word card"),

        ["settings.live.interval"] = ("فاصله‌ی بررسی صفحه", "Screen check interval"),
        ["settings.live.interval.hint"] = ("کمتر = واکنش سریع‌تر، کمی CPU بیشتر", "Lower = faster reaction, slightly more CPU"),
        ["settings.live.opacity"] = ("پوشانندگی جعبه‌های ترجمه", "Opacity of the translation boxes"),
        ["settings.live.fontscale"] = ("اندازه‌ی فونت روی صفحه", "On-screen font size"),

        ["settings.engine.openai"] = ("سازگار با OpenAI", "OpenAI-compatible"),
        ["settings.engine.hint.claude"] = ("کیفیت بالاتر برای متن‌های ادبی و فنی. اگر در دسترس نبود، خودکار به Google برمی‌گردد.",
                                           "Better quality on literary and technical text. Falls back to Google automatically."),
        ["settings.engine.hint.openai"] = ("هر سرویسی با API سازگار با OpenAI. اگر در دسترس نبود، خودکار به Google برمی‌گردد.",
                                           "Any service with an OpenAI-compatible API. Falls back to Google automatically."),
        ["settings.engine.hint.google"] = ("سریع و رایگان، بدون نیاز به کلید. برای ترجمه‌ی زنده هم مناسب است.",
                                           "Fast and free, no key needed. Good for live translation too."),
        ["settings.engine.openai.hint"] = ("OpenAI، OpenRouter، Groq، DeepSeek، Ollama/LM Studio محلی، یا هر پروکسی که فرمت chat/completions را بفهمد.",
                                           "OpenAI, OpenRouter, Groq, DeepSeek, a local Ollama/LM Studio, or any proxy that speaks chat/completions."),
        ["settings.engine.openai.title"] = ("سرویس سازگار با OpenAI", "OpenAI-compatible service"),
        ["settings.apikey"] = ("کلید API", "API key"),
        ["settings.model"] = ("مدل", "Model"),
        ["settings.test"] = ("تست اتصال", "Test connection"),
        ["settings.test.missing"] = ("کلید یا مدل وارد نشده.", "The key or the model is missing."),
        ["settings.test.running"] = ("در حال تست…", "Testing…"),
        ["settings.hotkey.duplicate"] = ("دو میان‌بر یکسان انتخاب شده‌اند.", "Two shortcuts are set to the same combination."),

        ["settings.about.tagline"] = ("هر متنی روی صفحه را با یک میان‌بر بخوان و ترجمه کن؛ یا یک ناحیه را زنده ترجمه کن تا صفحه انگار از اول فارسی نوشته شده باشد.",
                                      "Read and translate any text on your screen with a shortcut — or translate a region live, so the page looks like it was written in your language from the start."),
        ["settings.about.stack"] = ("OCR: موتور داخلی ویندوز (آفلاین)  ·  ترجمه: Google / Claude / سازگار با OpenAI  ·  فونت: Vazirmatn (SIL OFL)  ·  ساخته‌شده با .NET و WPF",
                                    "OCR: the built-in Windows engine (offline)  ·  Translation: Google / Claude / OpenAI-compatible  ·  Font: Vazirmatn (SIL OFL)  ·  Built with .NET and WPF"),
        ["settings.about.keys"] = ("کلیدهای API فقط روی همین سیستم و به‌صورت رمزشده (DPAPI) ذخیره می‌شوند.",
                                   "API keys are stored on this machine only, encrypted with DPAPI."),
        ["settings.about.by"] = ("ساخته‌ی ", "Made by "),
        ["settings.about.welcome"] = ("نمایش دوباره‌ی راهنمای شروع", "Show the getting-started guide again"),
        ["settings.version"] = ("نسخه {0}", "Version {0}"),
    };
}

/// <summary>{ui:L key} — resolves a string once, when the XAML is loaded.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LExtension : MarkupExtension
{
    public LExtension() { }
    public LExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}

/// <summary>FlowDirection="{ui:Flow}" — mirrors a window to match the interface language.</summary>
[MarkupExtensionReturnType(typeof(FlowDirection))]
public sealed class FlowExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Flow;
}
