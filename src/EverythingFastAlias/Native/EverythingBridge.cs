using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EverythingFastAlias.Models;
using Microsoft.Win32;

namespace EverythingFastAlias.Native
{
    public class EverythingBridge
    {
        public const uint EVERYTHING_ERROR_IPC = 1;

        public static bool IsEverythingRunning()
        {
            // 임의로 쿼리를 날려서 IPC 에러가 발생하지 않는지 체크하는 것이 가장 정확함
            EverythingSdk.Everything_SetSearchW("test");
            EverythingSdk.Everything_SetMax(1);
            EverythingSdk.Everything_QueryW(false);
            
            var err = EverythingSdk.Everything_GetGetLastError();
            return err != EVERYTHING_ERROR_IPC;
        }

        public static bool StartEverythingEngine()
        {
            // 1. 레지스트리에서 Everything.exe 경로 조회
            string? exePath = GetEverythingExePathFromRegistry();

            // 2. 레지스트리에 없으면 기본 설치 경로 탐색
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                var defaultPath = @"C:\Program Files\Everything\Everything.exe";
                if (File.Exists(defaultPath))
                {
                    exePath = defaultPath;
                }
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false; // 실행 파일 못 찾음
            }

            try
            {
                // Everything을 백그라운드 서비스 및 프로세스로 구동
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-startup", // 시작프로그램 형태로 백그라운드로 띄움
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);

                // 엔진이 IPC 윈도우를 만들고 준비될 때까지 대기 (최대 3초)
                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(200);
                    if (IsEverythingRunning())
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string? GetEverythingExePathFromRegistry()
        {
            try
            {
                // App Paths 레지스트리에서 탐색
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Everything.exe");
                return key?.GetValue("")?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static List<SearchResultItem> Search(string processedQuery, SearchOptions options)
        {
            var results = new List<SearchResultItem>();

            // 1. 검색 옵션 주입
            EverythingSdk.Everything_SetMatchCase(options.MatchCase);
            EverythingSdk.Everything_SetMatchWholeWord(options.MatchWholeWord);
            EverythingSdk.Everything_SetRegex(options.UseRegex);
            
            // 2. 최대 조회 개수 설정
            EverythingSdk.Everything_SetMax(EverythingFastAlias.Config.AppConstants.DllConfig.DefaultMaxResults);
            EverythingSdk.Everything_SetOffset(0);

            // 3. 쿼리 전달
            EverythingSdk.Everything_SetSearchW(processedQuery);

            // 4. 실행 (동기형 쿼리)
            bool success = EverythingSdk.Everything_QueryW(true);
            if (!success)
            {
                var err = EverythingSdk.Everything_GetGetLastError();
                if (err == EVERYTHING_ERROR_IPC)
                {
                    throw new InvalidOperationException("Everything 엔진이 꺼져 있거나 연결에 실패했습니다.");
                }
                return results;
            }

            // 5. 결과 목록 가공
            uint numResults = EverythingSdk.Everything_GetNumResults();
            for (uint i = 0; i < numResults; i++)
            {
                var name = EverythingSdk.GetResultFileName(i);
                var path = EverythingSdk.GetResultPath(i);
                
                EverythingSdk.Everything_GetResultSize(i, out long size);
                EverythingSdk.Everything_GetResultDateModified(i, out long fileTime);
                bool isFolder = EverythingSdk.Everything_IsFolderResult(i);

                var ext = isFolder ? string.Empty : Path.GetExtension(name).TrimStart('.');

                results.Add(new SearchResultItem
                {
                    Name = name,
                    Path = path,
                    Size = size,
                    ModifiedDate = DateTime.FromFileTime(fileTime),
                    Extension = ext,
                    IsFolder = isFolder
                });
            }

            return results;
        }
    }
}
