// Ignore Spelling: Keyless

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;

namespace Transport_Support
{
    public class WayPoint
    {
        public string Group { get; set; } = String.Empty;
        public string HubUrl { get; set; } = String.Empty;
        public string PassPhrase { get; set; } = String.Empty;
        public string SecretDate { get; set; } = String.Empty;
        public string Pattern { get; set; } = String.Empty;
        public string Color { get; set; } = String.Empty;
        public string Prompts
        {
            get { return string.Concat(Color, " ", Pattern); }
        }
        public bool Reflect { get; set; } = false;
        public bool Complete
        {
            get
            {
                return (Group.Length > 0 &&
                    HubUrl.Length > 0 &&
                    PassPhrase.Length > 0 &&
                    SecretDate.Length > 0 &&
                    Pattern.Length > 0 &&
                    Color.Length > 0);
            }
        }

        public string display(string desc)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Concat(desc, " Group: ", Group));
            sb.AppendLine(string.Concat(desc, " HubUrl: ", HubUrl));
            //sb.AppendLine(string.Concat(desc, " PassPhrase: ", PassPhrase));
            //sb.AppendLine(string.Concat(desc, " SecretDate: ", SecretDate));
            sb.AppendLine(string.Concat(desc, " Prompts: ", Color, " ", Pattern));
            return sb.ToString();
        }
    }

    public class EndPoint : IRouteProvider
    {
        private ILogger<EndPoint> logger;
        public ILoggerFactory loggerFactory;
        public WayPoint Params = new WayPoint();
        public readonly Purpose role = Purpose.none;
        public readonly RoutedMessageAction _receiptAction = RoutedMessageAction.ReportError;
        private Exception? _Ex = null;
        
        public Exception? Ex
        {
            get { return _Ex; }
        }
        public RoutedMessageAction ReceiptAction
        {
            get { return _receiptAction; }
        }
        public bool Valid
        {
            get { return Params.Complete; }
        }

        public EndPoint(ILoggerFactory loggerFactoryIn, ILogger<EndPoint> loggerIn, IUsage usage)
        {
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;
            
            using (logger.BeginScope("Constructing End Point"))
            {
                try
                {
                    role = usage.ProgramPurpose;
                    _receiptAction = TransportSupport.getReceivedActionFromRole(role);
                }
                catch (Exception ex)
                {
                    _Ex = ex;
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }


            if (usage.ProgramPurpose == Purpose.Transmitter || usage.ProgramPurpose == Purpose.Receiver)
            {
                DirectoryInfo HomeDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                List<DirectoryInfo> SubDirectories = HomeDirectory.GetDirectories("From").ToList<DirectoryInfo>();
                SubDirectories.AddRange(HomeDirectory.GetDirectories("To").ToList<DirectoryInfo>());
                if (SubDirectories.Count > 1)
                {
                    logger.LogError("Too many subdirectories");
                }
                else
                {
                    if (SubDirectories.Count < 1)
                    {
                        logger.LogError("Missing subdirectory");
                    }
                    else
                    {
                        FileInfo[] ConfigFiles = SubDirectories[0].GetFiles("*.json");
                        if (ConfigFiles.Length > 1)
                        {
                            logger.LogError("Too many config files");
                        }
                        else
                        {
                            if (ConfigFiles.Length < 1)
                            {
                                logger.LogError("Missing config file");
                            }
                            else
                            {
                                IConfigurationBuilder cbuilder = new ConfigurationBuilder().AddJsonFile(ConfigFiles[0].FullName, false, true);
                                IConfiguration Config = cbuilder.Build();

                                ConfigurationBinder.Bind(Config, "Root", Params);

                                logger.LogDebug(Params.display("Endpoint"));

                                if (!Params.Complete)
                                {
                                    logger.LogError("Config incomplete");
                                }
                                else
                                {
                                    logger.LogDebug("Config complete");
                                }

                            }
                        }
                    }
                }
            }
            else
            {
                logger.LogError("Endpoints are only for transmitters and receivers");
            }
        }

        public WayPoint GetFrom(string group = "")
        {
            return Params;
        }

        public WayPoint GetTo(string group = "")
        {
            return Params;
        }
               
        public bool HasRoute(string group = "")
        {
            return true;
        }
        public bool Reflect(string group = "")
        {
            return false;
        }

        public string Snapshot()
        {
            return string.Join(Environment.NewLine, Spill()); 
        }


        public List<string> Spill()
        {
            List<string> litany = new List<string>();

            litany.Add(Params.display("Endpoint for " + role.ToString() ));

            litany.Add("Receipt Action will be " + _receiptAction.ToString());

            if (Valid)
            {
                litany.Add("Endpoint is valid");
            }
            else
            {
                litany.Add("Endpoint is invalid");
            }

            if (!(_Ex == null))
            {
                litany.Add("There was an Exception:");
                litany.Add(_Ex.ToString());
            }

            return litany;
        }

    }

    public enum RouteStatus
    {
        Valid,
        Folder_Missing,
        Origin_Directory_Missing,
        Destination_Directory_Missing,
        Origin_File_Missing,
        Destination_File_Missing,
        Too_Many_Origin_Files,
        Too_Many_Destination_Files,
        Origin_Incomplete,
        Destination_Incomplete,
        Unknown
    }
    public class Route
    {
        public string Name = string.Empty;
        public RouteStatus Status = RouteStatus.Unknown;
        public WayPoint Origin = new WayPoint();
        public WayPoint Destination = new WayPoint();

        private ILogger<Route> logger;
        public bool GoodForRelay
        {
            get { return (Status == RouteStatus.Valid && Origin.HubUrl != Destination.HubUrl); }
        }
        public bool GoodForReflectorOrHubOrClient
        {
            get { return (Status == RouteStatus.Valid && Origin.HubUrl == Destination.HubUrl); }
        }

        public Route(ILoggerFactory loggerFactory, DirectoryInfo directory)
        {
            logger = loggerFactory.CreateLogger<Route>();

            using (logger.BeginScope("Constructing Route from info about " + directory.FullName))
            {
                try
                {
                    Init(directory);
                    
                    logger.LogDebug(Snapshot());

                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public Route(ILoggerFactory loggerFactory, string dirName)
        {
            logger = loggerFactory.CreateLogger<Route>();

            using (logger.BeginScope("Constructing route from " + dirName))
            {
                try
                {
                    DirectoryInfo directory = new DirectoryInfo(dirName);

                    Init(directory);

                    logger.LogDebug(Snapshot());
                   
                    logger.LogDebug("Constructed");
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Encountered Exception");
                }
            }
        }

        public void Init(DirectoryInfo directory)
        {

            Name = directory.Name;

            if (directory.Exists)
            {
                //logger.LogDebug(Name + " exists");
                DirectoryInfo[] ToDirectories = directory.GetDirectories("To");
                DirectoryInfo[] FromDirectories = directory.GetDirectories("From");

                if (ToDirectories.Length * FromDirectories.Length == 1)
                {
                    // directories are good
                    FileInfo[] FromFiles = FromDirectories[0].GetFiles("*.json");
                    FileInfo[] ToFiles = ToDirectories[0].GetFiles("*.json");

                    if (FromFiles.Length * ToFiles.Length == 1)
                    {

                        IConfigurationBuilder cbuilder1 = new ConfigurationBuilder().AddJsonFile(FromFiles[0].FullName, false, true);
                        IConfiguration FromConfig = cbuilder1.Build();

                        IConfigurationBuilder cbuilder2 = new ConfigurationBuilder().AddJsonFile(ToFiles[0].FullName, false, true);
                        IConfiguration ToConfig = cbuilder2.Build();

                        ConfigurationBinder.Bind(FromConfig, "Root", Origin);

                        ConfigurationBinder.Bind(ToConfig, "Root", Destination);

                        if (Origin.Complete)
                        {
                            if (Destination.Complete)
                            {
                                Status = RouteStatus.Valid;
                                if (Origin.HubUrl == Destination.HubUrl)
                                {
                                    Destination.Reflect = true;
                                }
                            }
                            else
                            {
                                Status = RouteStatus.Destination_Incomplete;
                            }
                        }
                        else
                        {
                            Status = RouteStatus.Origin_Incomplete;
                        }
                    }
                    else
                    {
                        if (FromFiles.Length == 1)
                        {
                            // to is wrong
                            if (ToFiles.Length > 1)
                            {
                                Status = RouteStatus.Too_Many_Destination_Files;
                            }
                            else
                            {
                                Status = RouteStatus.Destination_File_Missing;
                            }
                        }
                        else
                        {
                            // from is wrong
                            if (FromFiles.Length > 1)
                            {
                                Status = RouteStatus.Too_Many_Origin_Files;
                            }
                            else
                            {
                                Status = RouteStatus.Origin_File_Missing;
                            }
                        }
                    }

                }
                else
                {
                    if (FromDirectories.Length == 1)
                    {
                        // to is wrong and can only be missing
                        Status = RouteStatus.Destination_Directory_Missing;
                    }
                    else
                    {
                        // from is wrong and can only be missing
                        Status = RouteStatus.Origin_Directory_Missing;
                    }
                }
            }
            else
            {
                Status = RouteStatus.Folder_Missing;
                //logger.LogDebug(Name + " does not exist");
            }
        }

        public string Snapshot()
        {
            return string.Join(Environment.NewLine, Spill());
        }

        public List<string> Spill()
        {
            List<string> litany = new List<string>();

            litany.Add( "Route " + Name + " Status is " + Status.ToString());
            litany.Add(Origin.display(Name + " From"));
            litany.Add(Destination.display(Name + " To"));
                if (GoodForReflectorOrHubOrClient)
                {
                litany.Add("Good for Reflector or Hub");
                }
                if (GoodForRelay)
                {
                litany.Add("Good for Relay");
                }
                
            return litany;
        }
    }

    public class RouteList : IRouteProvider
    {
        private ILogger<RouteList> logger;
        public ILoggerFactory loggerFactory;
        public Dictionary<string, Route> routes = new Dictionary<string, Route>();
        public string FromUrl = string.Empty;
        public readonly Purpose role = Purpose.none;
        private readonly RoutedMessageAction _receiptAction = RoutedMessageAction.ReportError;

        private Exception? _Ex = null;

        public Exception? Ex
        {
            get { return _Ex; }
        }
        
        public RoutedMessageAction ReceiptAction
        {
            get { return _receiptAction; }
        }

        public RouteList(ILoggerFactory loggerFactoryIn, ILogger<RouteList> loggerIn, IUsage usage)
        {
            logger = loggerIn;
            loggerFactory = loggerFactoryIn;

            role = usage.ProgramPurpose;
            _receiptAction = TransportSupport.getReceivedActionFromRole(role);

            DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);

            logger.LogDebug("Looking for routes in " + di.FullName);

            foreach (DirectoryInfo PossibleRoute in di.GetDirectories("?oute*"))
            {
                logger.LogDebug("Found route " + PossibleRoute.Name);
                Route candidate = new Route(loggerFactory, PossibleRoute);
                if (candidate.Status == RouteStatus.Valid)
                {
                    if (string.IsNullOrEmpty(FromUrl) || (FromUrl == candidate.Origin.HubUrl))
                    {
                        FromUrl = candidate.Origin.HubUrl;
                        if ((usage.ProgramPurpose == Purpose.Hub || usage.ProgramPurpose == Purpose.Reflector || 
                            usage.ProgramPurpose == Purpose.Client) && candidate.GoodForReflectorOrHubOrClient)
                        {                            
                            if (routes.ContainsKey(candidate.Origin.Group))
                            {
                                logger.LogError(string.Concat("Route ", candidate.Name, " is a repeat of group " + candidate.Origin.Group));
                                continue;
                            }
                            else
                            {
                                routes.Add(candidate.Origin.Group, candidate);
                                logger.LogDebug("Added Route " + candidate.Name);
                                if (usage.ProgramPurpose == Purpose.Reflector)
                                {
                                    break; // reflectors use the first valid route
                                }
                            }
                        }
                        else
                        {
                            if (usage.ProgramPurpose == Purpose.Relay && candidate.GoodForRelay)
                            {
                                routes.Add(candidate.Origin.Group, candidate);
                                logger.LogDebug("Added Route " + candidate.Name);

                                break; // relays use the first valid route
                            }
                            else
                            {
                                logger.LogError(string.Concat("Route ", candidate.Name, " is not good for " + usage.ProgramPurpose.ToString()));
                            }
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(FromUrl))
                        {
                            logger.LogError(string.Concat("Route ", candidate.Name, " is missing a starting Url"));
                        }
                        else
                        {
                            logger.LogError(string.Concat("Route ", candidate.Name, " starts at the wrong Url"));
                            
                        }
                    }           
                }
                else
                {
                    logger.LogError(string.Concat("Route ", candidate.Name, " has ", candidate.Status.ToString().Replace('_', ' ')));
                }
            } 

            logger.LogDebug(routes.Count.ToString() + " valid routes");

        }

        public string Snapshot()
        {
             return string.Join(Environment.NewLine, Spill()); 
        }

        public List<string> Spill()
        {
            List<string> litany = new List<string>();

            litany.Add("Route list for " + role.ToString());

            litany.Add("Receipt Action will be " + _receiptAction.ToString());

            if (routes.Count > 0)
            {
                litany.Add("There are " + routes.Count.ToString() + " routes");

                foreach (Route r in routes.Values)
                {
                    litany.Add(r.Snapshot());
                }
            }
            else
            {
                litany.Add("There are No Routes");
            }

            if (Valid)
            {
                litany.Add("Route list is valid");
            }
            else
            {
                litany.Add("Route List is invalid");
            }

            if (!(_Ex == null))
            {
                litany.Add("There was an Exception:");
                litany.Add(_Ex.ToString());
            }



            return litany;
        }





        public bool Valid
        {
            get { return (routes.Count > 0); }
        }

        public WayPoint GetFrom(string group = "")
        {
            WayPoint result = new WayPoint();

            if (routes.Count > 0)
            {
                if (string.IsNullOrEmpty(group))
                {
                    string firstKey = routes.Keys.First<string>();
                    result = routes[firstKey].Origin;
                }
                else
                {
                    if (routes.ContainsKey(group))
                    {
                        return routes[group].Origin;
                    }
                }
            }

            return result;
        }

        public WayPoint GetTo(string group = "")
        {
            WayPoint result = new WayPoint();

            if (routes.Count > 0)
            {
                if (string.IsNullOrEmpty(group))
                {
                    string firstKey = routes.Keys.First<string>();
                    result = routes[firstKey].Destination;
                }
                else
                {
                    if (routes.ContainsKey(group))
                    {
                        result = routes[group].Destination;
                    }
                }
            }

            return result;
        }


        public bool HasRoute(string group = "")
        {
            bool result = false;

            if (routes.Count > 0)
            {
                if (string.IsNullOrEmpty(group) || routes.ContainsKey(group))
                {
                    result = true;
                }          
            }

            return result;
        }       

        public bool Reflect(string group = "")
        {
            bool result = false;

            if (routes.Count > 0)
            {
                if (string.IsNullOrEmpty(group))
                {
                    string firstKey = routes.Keys.First<string>();
                    result = routes[firstKey].GoodForReflectorOrHubOrClient;
                }
                else
                {
                    if (routes.ContainsKey(group))
                    {
                        result = routes[group].GoodForReflectorOrHubOrClient;
                    }
                }
                
            }

            return result;
        }

        public string list()
        {
            StringBuilder sb = new StringBuilder();

            if (routes.Count > 0)
            {
                foreach (Route r in routes.Values)
                {
                    sb.AppendLine(r.Name);
                }
            }
            else
            {
                sb.AppendLine("No Routes");
            }

            return sb.ToString();
        }        
    }
}
