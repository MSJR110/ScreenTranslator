<div align="center">

<img src="src/ScreenTranslator/Assets/logo.png" alt="ScreenTranslator" width="96" />

# ScreenTranslator

«Circle to Search» برای ویندوز — هر متنی روی صفحه را با یک میان‌بر بخوان و ترجمه کن؛ یا یک ناحیه را **زنده** ترجمه کن تا صفحه انگار از اول فارسی نوشته شده باشد.

[![build](https://github.com/MSJR110/ScreenTranslator/actions/workflows/build.yml/badge.svg)](https://github.com/MSJR110/ScreenTranslator/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Windows 10/11](https://img.shields.io/badge/Windows-10%202004%2B-0078D4?logo=windows&logoColor=white)](#نصب)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](#ساخت-از-سورس)

[English](README.md) · [دانلود](https://github.com/MSJR110/ScreenTranslator/releases/latest)

<img src="docs/screenshots/live-overlay.png" alt="ترجمه‌ی زنده روی یک مقاله‌ی ویکی‌پدیا" width="820" />

</div>

---

## چه کار می‌کند

میان‌بر را بزن، دور هر چیزی روی صفحه خط بکش — ویدیو، PDF، بازی، عکس، پنجره‌ی خطا — و متنش ترجمه‌شده برمی‌گردد. بدون کپی‌پیست، بدون تب مرورگر، بدون پنجره‌ی اصلی: برنامه کنار ساعت ویندوز می‌نشیند و فقط با میان‌بر بیدار می‌شود.

**حالت زنده** جالب‌ترین بخش است: یک ناحیه (یا کل یک پنجره) را ثانیه‌ای یکی‌دو بار می‌خواند و ترجمه را *روی* متن اصلی می‌کشد — رنگ، بافت زمینه و ضخامت قلم را از خود صفحه نمونه‌برداری می‌کند تا نتیجه مثل متن اصلی به نظر برسد. اسکرول کن، کلیک کن، کارت را بکن؛ جعبه‌ها دنبالت می‌آیند. `Ctrl` را نگه دار تا اصل متن را ببینی.

| متن اصلی | ترجمه‌ی زنده |
|---|---|
| <img src="docs/screenshots/before.png" alt="مقاله‌ی اصلی انگلیسی" width="400" /> | <img src="docs/screenshots/after.png" alt="همان مقاله، ترجمه‌شده در جای خودش" width="400" /> |

## ویژگی‌ها

- **OCR داخلی ویندوز** — آفلاین، بدون GPU، ~۱۰۰ میلی‌ثانیه برای کل صفحه؛ فونت‌های ریز خودکار بزرگ می‌شوند.
- **ترجمه‌ی رایگان از همان اول** — Google بدون کلید. اختیاری: موتور هوش مصنوعی (Claude یا هر سرویس سازگار با OpenAI) با fallback خودکار به Google.
- **دیکشنری کلمه‌ای** — موس را روی کلمه بگذار و میان‌بر را بزن: تلفظ IPA، صدای طبیعی، معنی فارسی و تعریف انگلیسی از Wiktionary. همه رایگان و بدون کلید.
- **ترجمه‌ی متن انتخاب‌شده** — متن هایلایت‌شده در هر برنامه‌ای، بدون OCR (دقیق‌ترین حالت).
- **OCR به کلیپ‌بورد** — متن داخل عکس یا فریم ویدیو را بدون ترجمه بردار.
- **۱۱ زبان مقصد** — فارسی (پیش‌فرض)، انگلیسی، عربی، ترکی، آلمانی، فرانسوی، اسپانیایی، روسی، ایتالیایی، ژاپنی، چینی.
- **رابط کاربری دوزبانه** — فارسی و انگلیسی؛ خودکار از روی زبان ویندوز انتخاب می‌شود و در تنظیمات هم قابل تغییر است. متن ترجمه همیشه به جهت خودش خوانده می‌شود.
- **ظاهر ویندوز ۱۱** — Acrylic، گوشه‌های گرد، فونت Vazirmatn، تم تیره/روشن همراه با سیستم، چند مانیتور با DPI متفاوت.
- **سبک** — در حالت بیکار ۰٪ CPU و ~۱۰ مگ RAM، یک فایل اجرایی، بدون سرویس پس‌زمینه.
- **خصوصی** — تاریخچه روی همین سیستم می‌ماند، کلیدهای API با DPAPI رمز می‌شوند، هیچ چیز جز خودِ درخواست ترجمه جایی نمی‌رود.

## میان‌برها (قابل تغییر در تنظیمات)

| پیش‌فرض | کار |
|---|---|
| `Ctrl+Alt+T` | یک ناحیه بکش → پاپ‌آپ متن اصلی + ترجمه |
| `Ctrl+Alt+S` | متن انتخاب‌شده در هر برنامه‌ای را ترجمه کن (بدون OCR؛ دقیق‌ترین حالت) |
| `Ctrl+Alt+D` | **معنی کلمه‌ی زیر موس** — کارت کوچک با تلفظ IPA، صدای طبیعی، معنی فارسی و تعریف انگلیسی (Wiktionary) |
| `Ctrl+Alt+C` | یک ناحیه بکش → متنش بدون ترجمه به کلیپ‌بورد می‌رود (OCR خالص) |
| `Ctrl+Alt+L` | ترجمه‌ی زنده‌ی یک ناحیه — دوباره بزن تا تمام شود |
| `Ctrl+Alt+W` | ترجمه‌ی زنده‌ی پنجره‌ی فعال (با جابه‌جایی پنجره حرکت می‌کند) |
| نگه‌داشتن `Ctrl` | در حالت زنده، موقتاً متن اصلی را نشان می‌دهد |
| `Esc` | بستن پاپ‌آپ / لغو انتخاب |
| کشیدن لبه‌ی پاپ‌آپ | تغییر اندازه (به خاطر می‌ماند)؛ دابل‌کلیک روی سرتیتر = اندازه‌ی خودکار |

کلیک روی آیکون tray یک فلای‌اوت شیشه‌ای باز می‌کند: چهار عمل اصلی به شکل کاشی، ترجمه‌ی زنده (ناحیه / پنجره / کل صفحه)، تاریخچه، تنظیمات، راهنمای شروع.

<div align="center">
<img src="docs/screenshots/tray.png" alt="فلای‌اوت tray" width="290" />
<img src="docs/screenshots/popup.png" alt="پاپ‌آپ ترجمه" width="460" />
</div>

<div align="center">
<img src="docs/screenshots/word.png" alt="کارت دیکشنری" width="330" />
<img src="docs/screenshots/settings.png" alt="پنجره‌ی تنظیمات" width="420" />
</div>

## نصب

آخرین نسخه را از [صفحه‌ی Releases](https://github.com/MSJR110/ScreenTranslator/releases/latest) بردار:

- **Setup:** `ScreenTranslator-Setup-<ver>.exe` — نصب برای کاربر فعلی (بدون UAC)، گزینه‌ی اجرا در استارت‌آپ.
- **Portable:** `ScreenTranslator-<ver>-portable.zip` — باز کن و اجرا کن.

نیازمندی: ویندوز ۱۰ نسخه‌ی 2004 به بالا (x64) و **.NET 10 Desktop Runtime** (اگر نباشد، Setup لینک دانلود می‌دهد). برای نسخه‌ای که رانتایم داخلش باشد: `.\publish.ps1 -SelfContained`.

فایل اجرایی امضای دیجیتال (code signing) ندارد، پس بار اول SmartScreen هشدار می‌دهد — *More info* → *Run anyway*.

## ساخت از سورس

```powershell
.\publish.ps1 -Zip -Installer     # dist\ + installer\Output\
.\publish.ps1 -Run                # فقط exe و اجرا
.\publish.ps1 -SelfContained      # با رانتایم داخل بسته
.\tools\make-icon.ps1             # بازتولید Assets\app.ico و logo.png
```

نیازمندی: .NET 10 SDK؛ برای installer، Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

## معماری

```
src/ScreenTranslator/
├─ App.xaml.cs               هماهنگی: هات‌کی → capture → OCR → ترجمه → UI؛ تاریخچه، تنظیمات، خوش‌آمد
├─ App.Debug.cs              --selftest / --demo-* : تست و اسکرین‌شات بدون دخالت دست (popup|settings|history|welcome|live|live-file|word|lookup|selector|toast|tray)
├─ Services/
│  ├─ OcrService             Windows.Media.Ocr + بزرگ‌نمایی خودکار برای فونت ریز
│  ├─ TextLayout             خط‌های OCR → پاراگراف/بلوک؛ چسباندن خط‌های شکسته؛ فیلتر متن غیرقابل‌ترجمه
│  ├─ LiveSession            حلقه‌ی زنده: capture → تشخیص تغییر → OCR → ترجمه‌ی دسته‌ای → نمونه‌برداری
│  ├─ RegionPixels           خواندن پیکسل‌های ناحیه: رنگ متن/زمینه، ضخامت قلم (Bold)، بافت زمینه، لبه‌ی سطح
│  ├─ Translation/           Google (دو endpoint)، Claude (SDK رسمی)، OpenAI-compatible، Fallback، Cache، Factory
│  ├─ Hotkey / HotkeyManager میان‌برهای سراسری قابل تنظیم
│  ├─ HistoryStore           تاریخچه‌ی JSON (۵۰۰ مورد)
│  ├─ SecretStore            کلیدهای API با DPAPI رمز می‌شوند
│  ├─ SelectionReader        Ctrl+C شبیه‌سازی‌شده + بازگرداندن کلیپ‌بورد
│  ├─ SpeechService          تلفظ: صدای آنلاین Google برای متن کوتاه، صداهای ویندوز برای بقیه
│  └─ DictionaryService      Wiktionary REST (تعریف، مثال) + wikitext (IPA)؛ حل شکل‌های صرفی
├─ UI/
│  ├─ OverlayWindow          لایه‌ی click-through و خارج از capture؛ متن فارسی روی متن اصلی (بافت زمینه، لبه‌ی محو، Bold، حالت زیرنویس)
│  ├─ LivePillWindow         کنترل شناور حالت زنده
│  ├─ ResultWindow           پاپ‌آپ ترجمه (Acrylic، قابل تغییر اندازه)
│  ├─ WordWindow             کارت دیکشنری یک کلمه
│  ├─ ToastWindow            HUD کوچک بالای صفحه برای بازخورد (کپی شد، خطا…)
│  ├─ HighlightWindow        فلش کوتاه دور کلمه‌ی انتخاب‌شده
│  ├─ SettingsWindow         تنظیمات (تب‌ها: عمومی، میان‌برها، زنده، موتور، درباره)
│  ├─ HistoryWindow / WelcomeWindow / RegionSelectorWindow (هر مانیتور یک پنجره)
│  ├─ Controls.xaml          استایل کنترل‌ها (toggle، slider، combo، scrollbar، input)
│  ├─ Theme / Dpi / HotkeyBox / AppIcon
└─ Native/                   RegisterHotKey، SetWindowDisplayAffinity، DWM backdrop، DPI per-monitor
```

### چرا سبک می‌ماند

- حالت زنده صفحه را ۱–۲ بار در ثانیه می‌گیرد و فقط وقتی هش تصویر عوض شده باشد OCR می‌کند.
- ترجمه‌ها کش می‌شوند و بلوک‌های جدید در **یک** درخواست فرستاده می‌شوند.
- لایه‌ی ترجمه با `WDA_EXCLUDEFROMCAPTURE` از capture حذف می‌شود، پس برنامه هرگز خروجی خودش را دوباره نمی‌خواند.

### کار روی رابط کاربری

هر پنجره را می‌شود بدون دست‌زدن به موس اسکرین‌شات گرفت:

```powershell
.\dist\ScreenTranslator.exe --demo-popup shot.png
.\dist\ScreenTranslator.exe --demo-settings shot.png live
.\dist\ScreenTranslator.exe --demo-live-file page.png out.png   # کل مسیر حالت زنده روی یک PNG
```

حالت‌ها: `--demo-popup|settings|history|welcome|word|lookup|selector|toast|tray|live|live-file`؛ متغیر `ST_THEME=light|dark` تم را عوض می‌کند.

## حریم خصوصی

- OCR روی همین سیستم اجرا می‌شود.
- فقط متن تشخیص‌داده‌شده به سرویس ترجمه‌ی انتخابی‌ات می‌رود (پیش‌فرض: Google، یا موتور خودت).
- تاریخچه یک فایل JSON در پروفایل کاربر است و می‌شود خاموشش کرد؛ کلیدهای API با DPAPI رمز می‌شوند.
- بدون telemetry، بدون analytics، بدون پینگ آپدیت.

## مشارکت

Issue و Pull Request خوش‌آمد است — مخصوصاً ترجمه‌ی رابط کاربری به زبان‌های دیگر، زبان‌های OCR بیشتر، و گزارش سایت‌ها یا برنامه‌هایی که حالت زنده رویشان خوب در نمی‌آید. کد WPF ساده است بدون فریم‌ورک MVVM؛ هر پنجره یک فایل کوچک مستقل.

## مجوزها

پروژه: [MIT](LICENSE).

فونت Vazirmatn — SIL Open Font License 1.1 (`src/ScreenTranslator/Assets/Fonts/LICENSE-Vazirmatn.txt`).
