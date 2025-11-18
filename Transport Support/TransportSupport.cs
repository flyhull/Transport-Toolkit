using Common_Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using MimeDetective.Storage;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Transport_Support
{
    
    
    public class TransportSupport
    {
        private const int KeepAliveSeconds = 10;

        private const int MinimumTransitTimeInSeconds = 20;

        private const int MaximumTransitTimeInSeconds = 30 * 60;

        private const int MinimumProcessingTimeInSeconds = 0;

        private const int MaximumProcessingTimeInSeconds = 45;

        private const int MinimumDelayInSeconds = 10;

        private const int MaximumDelayInSeconds = 20;

        private const int MinimumNumberHops = 8;

        private const int MaximumNumberHops = 12;

        private const int MaxDecryptionSeconds = 20;

        public static int getMaxDecryptionSeconds()
        {
            return MaxDecryptionSeconds;
        }
        public static int getKeepAliveSeconds()
        {
            return KeepAliveSeconds;
        }

        public static int getMaximumDelayInSeconds()
        {
            return MaximumDelayInSeconds;
        }

        public static int getMinimumTransitTimeInSeconds()
        {
            return MinimumTransitTimeInSeconds;
        }

        public static int getMaximumTransitTimeInSeconds()
        {
            return MaximumTransitTimeInSeconds;
        }

        public static int getMaximumProcessingTimeInSeconds()
        {
            return MaximumProcessingTimeInSeconds;
        }

        public static int getMinimumDelayInSeconds()
        {
            return MinimumDelayInSeconds;
        }

        public static int getRandomDelayInSeconds()
        {
            return new Random(DateTime.Now.Millisecond).Next(getMinimumDelayInSeconds(), getMaximumDelayInSeconds());
        }

        public static int getMaximumLookbackTimeInSeconds()
        {
            return MaximumDelayInSeconds + MaximumProcessingTimeInSeconds;
        }

        public static string getRandomSender()
        {
            return TransportSupport.getSenderFromConnectionId(CommonSupport.GetRandomString(24, 8));
        }

        public static Byte[] GetPassphrase(string reason)
        {
            string? passPhrase = string.Empty;

            while (string.IsNullOrEmpty(passPhrase))
            {
                Console.Write("Please enter " + reason + " passphrase ");
                passPhrase = Console.ReadLine();
            }

            return Encoding.UTF8.GetBytes(passPhrase);
        }

        public static DateTime GetSecretDateTime(string reason)
        {
            DateTime secretDateTime = DateTime.UtcNow;
            bool haveSecretDateTime = false;
            string? dateTimeString = string.Empty;
            CultureInfo culture = CultureInfo.InvariantCulture;
            DateTimeStyles style = DateTimeStyles.AssumeUniversal;

            while (!haveSecretDateTime)
            {
                Console.Write("Please enter " + reason + " secret date and optional time using a standard .NET date and time format ");
                dateTimeString = Console.ReadLine();
                //Console.WriteLine("");
                haveSecretDateTime = DateTime.TryParse(dateTimeString, culture, style, out secretDateTime);
            }

            return secretDateTime;
        }
        public static RoutedMessageAction getReceivedActionFromRole(Purpose role)
        {
            RoutedMessageAction result = RoutedMessageAction.ReportError;
            switch (role)
            {
                case Purpose.Relay:
                case Purpose.Receiver:
                    result = RoutedMessageAction.DoNothing;
                    break;
                case Purpose.Hub:
                    result = RoutedMessageAction.SendImmediate;
                    break;
                default:
                    result = RoutedMessageAction.CacheAndSend;
                    break;
            }
            return result;
        }

        public static string getSenderFromConnectionId(string connectionId)
        {
            return BitConverter.ToString(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(connectionId))).Replace("-", string.Empty);
        }

        public static string getBase64MessageIdentifier(string message, byte[] fuzz, string fromGroup = "")
        {
            string result = string.Empty;

            using (MemoryStream ms = new  MemoryStream())
            {
                ms.Write(fuzz);
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes(fromGroup)));
                ms.Write(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(message)));
                result = BitConverter.ToString(MD5.HashData(ms.ToArray())).Replace("-","");
            }

            return result;
        }

        public static string getDecryptedMessageIdentifier(ref ResultObject message, byte[] fuzz, string fromGroup = "")
        {
            string result = string.Empty;

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fuzz);
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes(fromGroup)));
                ms.Write(MD5.HashData(message.Bytes));
                result = BitConverter.ToString(MD5.HashData(ms.ToArray()));
            }

            return result;
        }

        public static string getMessageIdentifier(ref byte[] message, byte[] fuzz, string fromGroup = "")
        {
            string result = string.Empty;

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(fuzz);
                ms.Write(MD5.HashData(Encoding.UTF8.GetBytes(fromGroup)));
                ms.Write(MD5.HashData(message));
                result = BitConverter.ToString(MD5.HashData(ms.ToArray())).Replace("-", string.Empty);
            }

            return result;
        }

        public static string getWorkingDirectory()
        {
            return Environment.CurrentDirectory ?? "an unknown directory";
        }

        public static string getMyProgramsNodeNameAfterBuild(IConfiguration configuration)
        {
            string myName = configuration["MyName"] ?? "unknown";
            Console.WriteLine("Node name is " + myName + " and it is running " + Environment.ProcessPath);
            Console.Title = myName;
            return myName;
        }
        public static string getMyProgramsNodeNameBeforeBuild()
        {
            IConfigurationBuilder cbuilder = new ConfigurationBuilder().AddJsonFile(Path.Combine(Environment.CurrentDirectory, "appsettings.json"), false, true);
            Console.WriteLine("Retrieving configuration(s) from:");
            foreach (IConfigurationSource source in cbuilder.Sources)
            {
                Console.WriteLine(source.ToString());
            }
            IConfigurationRoot croot = cbuilder.Build();
            string myName = croot["MyName"] ?? "unknown";
            Console.WriteLine("Node name is " + myName + " and it is running " + Environment.ProcessPath);
            Console.Title = myName;
            return myName;

        }
        public static string describeMessageString(string Base64Message)
        {
            if (Base64Message.Length > 0)
            {
                Int32 endSize = int.Min(18, Base64Message.Length);
                StringBuilder sb = new StringBuilder();
                sb.Append("Message is " + Base64Message.Length.ToString() + " characters long");
                sb.Append(" and starts with " + Base64Message.Substring(0, endSize));
                sb.Append(" and ends with " + Base64Message.Substring(Base64Message.Length - 1 - endSize, endSize));
                sb.Append(" and has a hash of " + BitConverter.ToString(MD5.HashData(Encoding.UTF8.GetBytes(Base64Message))));
                return sb.ToString();
            }
            else
            {
                return "no data";
            }
        }

        public static bool DirectoryIsWritable(DirectoryInfo dir)
        {           
            try
            {
                DirectoryInfo[] found = dir.GetDirectories("test");
                if (found.Length > 0)
                {
                    foreach (DirectoryInfo item in found)
                    {
                        item.Delete();
                    }
                    dir.Refresh();

                    found = dir.GetDirectories("test");
                    if (found.Length > 0)
                    {
                        return false;  //cannot delete
                    }
                }
                
                dir.CreateSubdirectory("test");
                dir.Refresh();

                found = dir.GetDirectories("test");
                if (found.Length == 1)
                {
                    found.First<DirectoryInfo>().Delete();
                    dir.Refresh();

                    found = dir.GetDirectories("test");
                    
                    if (found.Length > 0)
                    {
                        return false;  //cannot delete
                    }
                    else
                    {
                        return true;  //can create and delete
                    }                    
                }
                else
                {
                    return false;  //cannot create
                }
            } 
            catch
            {
                return false; // blew up trying to create or delete
            }
        }
    }
}
