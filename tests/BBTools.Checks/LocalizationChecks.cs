using System.Globalization;
using System.IO;
using BBTools;
internal static partial class Program
{
    private static void LocalizationChecks()
    {
        var ui=CultureInfo.CurrentUICulture;var numbers=CultureInfo.CurrentCulture;
        try
        {
            Check(Translations.English.Count>=500&&Translations.Languages.Length==28,"English catalog and all 28 Crowdin languages are embedded");
            Check(new AppLanguage("zh-Hans", "简体中文").ToString()=="简体中文 — 100%"&&new AppLanguage("fr","Français").DisplayName==$"Français — {Translations.Coverage("fr")}%","language picker displays each native name and its translation percentage");
            Check(Translations.Resolve("auto","zh-CN")=="zh-Hans"&&Translations.Resolve("auto","zh-TW")=="zh-Hant"&&Translations.Resolve("auto","pt-PT")=="pt-PT"&&Translations.Resolve("auto","unknown")=="en","locale mapping preserves Chinese scripts and Portuguese variants with English fallback");
            foreach(var language in Translations.Languages){Translations.Select(language.Code);Check(!string.IsNullOrWhiteSpace(Translations.T("language.heading"))&&Translations.Coverage(language.Code) is >=0 and <=100,$"{language.Code} has catalog or English fallback");}
            Translations.Select("zh-Hans");Check(Translations.T("language.heading")=="界面语言"&&Translations.Format("app.version","0.6.3")=="版本 0.6.3"&&Translations.Coverage("zh-Hans")==100,"complete Simplified Chinese draft and formatted values resolve from resources");
            Translations.Select("fr");Check(Translations.Coverage("fr")==100&&Translations.Format("loot.bonus",250).Contains("250")&&Translations.T("language.heading")=="LANGUE DE L’INTERFACE","complete French draft is embedded and formats discovery values");
            Translations.Select("ar");Check(Translations.Coverage("ar")==100&&Translations.Format("loot.bonus",250).Contains("250")&&CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft&&new AppLanguage("ar","العربية").DisplayName.StartsWith("\u200E"),"complete Arabic draft is embedded with RTL culture, anchored picker percentage and discovery formatting");
            Check(CultureInfo.CurrentCulture==numbers,"UI language switching preserves number-input culture");
            Check(!Translations.Compatible("Value {0}","值 {1}")&&!Translations.Compatible("Value {0}","值 {0")&&Translations.Compatible("{0}: {1:N0}","{1:N0}：{0}"),"catalog validation rejects broken placeholders and accepts reordering");
            var folder=Path.Combine(Path.GetTempPath(),"BBTools-locale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{var path=Path.Combine(folder,"settings.json");new AppSettings(Language:"zh-Hans").Save(path);Check(AppSettings.Load(path).Language=="zh-Hans","interface language persists by stable locale code");Reject<ArgumentException>(()=>new AppSettings(Language:"../bad").Save(path),"invalid locale cannot select arbitrary resources");}
            finally{Directory.Delete(folder,true);}
        }
        finally{Translations.Select("en");CultureInfo.CurrentUICulture=ui;}
    }
}
