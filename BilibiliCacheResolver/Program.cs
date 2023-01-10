using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace BilibiliCacheResolver
{
    /// <summary>
    /// B站PC客户端缓存的视频是h.264格式的且对开头29个字节进行了混淆, 安卓客户端缓存的视频是h.265格式的可直接播放.
    /// </summary>
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("请输入B站视频合集根目录:");
            var folder = Console.ReadLine();
            if (folder == "exit")
            {
                return;
            }

            while (!Directory.Exists(folder))
            {
                Console.WriteLine("无此目录, 请重新输入:");
                folder = Console.ReadLine();
                if (folder == "exit")
                {
                    return;
                }
            }

            var directories = Directory.GetDirectories(folder, "c_*");
            if (directories.Length == 0)
            {
                return;
            }

            var title = ReadString(File.ReadAllText(Path.Combine(folder, directories[0], "entry.json")),
                "\"title\":\"");
            var destFolder = Path.Combine(folder, title);
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, true);
            }

            Directory.CreateDirectory(destFolder);

            foreach (var directory in directories)
            {
                var entryFile = Path.Combine(directory, "entry.json");
                var partTitle = ReadPartTitle(entryFile);
                var mediaDirectory = Path.Combine(directory, "80");
                Console.WriteLine(mediaDirectory);
                var processStartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = " -i video.m4s -i audio.m4s -codec copy Output.mp4",
                    WorkingDirectory = mediaDirectory,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                try
                {
                    using (var process = Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            Console.WriteLine("调用ffmpeg返回null");
                            Console.WriteLine("按回车退出");
                            Console.Read();
                            return;
                        }

                        // ffmpeg输出是error流, 下面这行必加, 不然process无法结束
                        process.StandardError.ReadToEnd();
                        process.WaitForExit();
                    }
                }
                catch (Exception e)
                {
                    if (e is Win32Exception we && we.ErrorCode == -2147467259)
                    {
                        Console.WriteLine("请先将ffmpeg.exe添加到系统环境变量路径中.");
                        Console.WriteLine("按回车退出");
                        Console.Read();
                        return;
                    }

                    Console.WriteLine(e);
                    Console.WriteLine("按回车退出");
                    Console.Read();
                    return;
                }

                var destFileName = Path.Combine(destFolder, partTitle + ".mp4");
                File.Move(Path.Combine(mediaDirectory, "Output.mp4"), destFileName);
                Console.WriteLine("创建视频文件:" + destFileName);
            }

            Console.WriteLine("视频文件成功提取到目录:" + destFolder);
            Console.WriteLine("按回车退出");
            Console.Read();
        }

        // 从json文件中读取视频标题
        private static string ReadPartTitle(string entryFile)
        {
            var s = File.ReadAllText(entryFile, Encoding.UTF8);
            var page = ReadInt(s, "\"page\":");
            var part = ReadString(s, "\"part\":\"");
            return "P" + page + " " + part;
        }

        private static string ReadInt(string s, string key)
        {
            var start = s.IndexOf(key, StringComparison.Ordinal) + key.Length;
            var end = s.IndexOf(',', start);
            return s.Substring(start, end - start).Trim();
        }

        private static string ReadString(string s, string key)
        {
            var start = s.IndexOf(key, StringComparison.Ordinal) + key.Length;
            var end = s.IndexOf("\",\"", start, StringComparison.Ordinal);
            return s.Substring(start, end - start).Trim();
        }

        // 解码PC客户端混淆的音视频文件. 因PC端为H.264编码, 暂未考虑处理PC端缓存. 这段代码留着, 以防以后万一用到.
        private static void DecodePcCache(string sourceFile, int bufferSize)
        {
            byte[] starts = new byte[29];
            using (var source = File.OpenRead(sourceFile))
            {
                var read = source.Read(starts, 0, starts.Length);
                if (read != starts.Length)
                {
                    return;
                }

                if (starts.SequenceEqual(new byte[]
                    {
                        0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x00, 0x00, 0x00, 0x24, 0x66, 0x74, 0x79,
                        0x70, 0x69, 0x73, 0x6F, 0x35, 0x00, 0x00, 0x00, 0x01, 0x61, 0x76, 0x63, 0x31
                    }))
                {
                    using (FileStream destination = File.OpenWrite(sourceFile + ".new"))
                    {
                        byte[] bytes =
                        {
                            0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x35, 0x00, 0x00, 0x00,
                            0x01
                        };
                        destination.Write(bytes, 0, bytes.Length);
                        byte[] buffer = new byte[bufferSize];
                        int count;
                        while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
                            destination.Write(buffer, 0, count);
                    }
                }
            }
        }
    }
}