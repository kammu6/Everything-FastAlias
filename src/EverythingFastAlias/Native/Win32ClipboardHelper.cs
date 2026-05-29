using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace EverythingFastAlias.Native
{
    public static class Win32ClipboardHelper
    {
        public static void CopyFilesToClipboard(IList<string> filePaths, bool isCut)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            // 파일 경로들의 실제 유효성 검사 및 정제
            var validPaths = new List<string>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    validPaths.Add(path);
                }
            }

            if (validPaths.Count == 0) return;

            try
            {
                var dataObject = new DataObject();

                // 1. 네이티브 파일 객체 리스트 등록 (CF_HDROP 포맷)
                dataObject.SetData(DataFormats.FileDrop, validPaths.ToArray());

                // 2. Preferred DropEffect 메모리 스트림 설정
                // DROPEFFECT_COPY = 1, DROPEFFECT_MOVE = 2 (잘라내기)
                byte[] dropEffect = new byte[] { (byte)(isCut ? 2 : 1), 0, 0, 0 };
                var ms = new MemoryStream(dropEffect);
                dataObject.SetData("Preferred DropEffect", ms);

                // 3. 클립보드에 데이터 오브젝트 셋 (유지 권장 옵션 true)
                Clipboard.SetDataObject(dataObject, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("클립보드 파일 등록에 실패했습니다. 상세: " + ex.Message, ex);
            }
        }
    }
}
