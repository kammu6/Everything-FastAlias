using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EverythingFastAlias.Config
{
    public static class FileExtensionConstants
    {
        public const string CategoryVideo = "영상";
        public const string CategoryAudio = "음악";
        public const string CategoryPicture = "사진";
        public const string CategoryDocument = "문서";
        public const string CategoryCode = "코드";
        public const string CategoryExecutable = "실행";
        public const string CategoryArchive = "압축";

        public const string VideoExtensions = "mp4;mkv;avi;wmv;flv;mov;mpg;mpeg;m4v;webm;vob;ogv;divx;ogm;m2ts;mts;tp;trp;3gp;3g2;asf;rm;rmvb;f4v;mxf;qt;m1v;m2v;mpv;mpe;wtv;dvr-ms;amv;k3g;skm;h264;hevc;bik;dav;ts";
        public const string AudioExtensions = "mp3;wav;flac;ogg;wma;m4a;aac;alac;aiff;ape;opus;mid;midi;mka;ac3;dts;ra;ram;amr;m4b;m4p";
        public const string PictureExtensions = "jpg;jpeg;jfif;png;gif;bmp;webp;tiff;tif;psd;ai;svg;ico;raw;cr2;nef;arw;dng;heic;heif;avif";
        public const string DocumentExtensions = "pdf;txt;hwp;hwpx;doc;docx;xls;xlsx;ppt;pptx;rtf;csv;tsv;odt;ods;odp;pages;numbers;key;epub;mobi;azw3;xps";
        public const string ExecutableExtensions = "exe;bat;cmd;msi;lnk;scr;sh;pyw;ps1;vbs;com;jar;appimage";
        public const string ArchiveExtensions = "zip;7z;rar;tar;gz;bz2;iso;alz;egg;xz;z;tgz;tbz2;cab;dmg;wim";
        public const string CodeExtensions = "ts;tsx;js;jsx;json;java;py;pyw;cpp;c;h;hpp;cs;html;css;scss;less;go;rs;sh;md;yml;yaml;xml;sql;php;rb;kt;swift;lua;vue;svelte;dart";

        public static readonly IReadOnlyList<string> AllCategories = new ReadOnlyCollection<string>(new[]
        {
            CategoryVideo,
            CategoryAudio,
            CategoryPicture,
            CategoryDocument,
            CategoryCode,
            CategoryExecutable,
            CategoryArchive
        });

        private static readonly Dictionary<string, string> DefaultMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { CategoryVideo, VideoExtensions },
            { CategoryAudio, AudioExtensions },
            { CategoryPicture, PictureExtensions },
            { CategoryDocument, DocumentExtensions },
            { CategoryCode, CodeExtensions },
            { CategoryExecutable, ExecutableExtensions },
            { CategoryArchive, ArchiveExtensions }
        };

        public static string GetDefaultExtensions(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return string.Empty;
            return DefaultMap.TryGetValue(category.Trim(), out var exts) ? exts : string.Empty;
        }

        public static IReadOnlyDictionary<string, string> GetAllDefaults()
        {
            return DefaultMap;
        }
    }
}
