﻿using log4net;
using OpenNos.Core;
using OpenNos.DAL;
using OpenNos.DAL.EF.Helpers;
using OpenNos.Data;
using OpenNos.GameObject;
using OpenNos.Handler;
using OpenNos.Master.Library.Client;
using OpenNos.Master.Library.Data;
using OpenNos.World.Resource;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenNos.World
{
    public class Program
    {
        #region Members

        private static EventHandler exitHandler;
        private static ManualResetEvent run = new ManualResetEvent(true);

        #endregion

        #region Delegates

        public delegate bool EventHandler(CtrlType sig);

        #endregion

        #region Enums

        public enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        #endregion

        #region Methods

        public static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            // initialize Logger
            Logger.InitializeLogger(LogManager.GetLogger(typeof(Program)));
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            Console.Title = string.Format(LocalizedResources.WORLD_SERVER_CONSOLE_TITLE, 0, 0, 0, 0);
            var port = Convert.ToInt16(ConfigurationManager.AppSettings["WorldPort"]);
            var text = $"WORLD SERVER v{fileVersionInfo.ProductVersion}dev - PORT : {port} by SystemX64 Team";

            var offset = Console.WindowWidth / 2 + text.Length / 2;
            var separator = new string('=', Console.WindowWidth);
            Console.WriteLine(separator + string.Format("{0," + offset + "}\n", text) + separator);

            // initialize api
            if (CommunicationServiceClient.Instance.Authenticate(ConfigurationManager.AppSettings["MasterAuthKey"]))
            {
                Logger.Log.Info(Language.Instance.GetMessageFromKey("API_INITIALIZED"));
            }

            // initialize DB
            if (DataAccessHelper.Initialize())
            {
                // register mappings for DAOs, Entity -> GameObject and GameObject -> Entity
                RegisterMappings();

                // initialilize maps
                ServerManager.Instance.Initialize();
            }
            else
            {
                Console.ReadKey();
                return;
            }

            // TODO: initialize ClientLinkManager initialize PacketSerialization
            PacketFactory.Initialize<WalkPacket>();
            var ip = ConfigurationManager.AppSettings["IPADDRESS"];
            if (bool.TryParse(ConfigurationManager.AppSettings["AutoReboot"], out bool autoreboot))
            {
                autoreboot = false;
            }

            try
            {
                exitHandler += ExitHandler;
                if (autoreboot)
                {
                    AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
                }

                NativeMethods.SetConsoleCtrlHandler(exitHandler, true);
            }
            catch (Exception ex)
            {
                Logger.Log.Error("General Error", ex);
            }

            portloop:
            try
            {
                NetworkManager<WorldEncryption> unused = new NetworkManager<WorldEncryption>(ConfigurationManager.AppSettings["IPADDRESS"], port, typeof(CommandPacketHandler), typeof(LoginEncryption), true);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10048)
                {
                    port++;
                    Logger.Log.Info("Port already in use! Incrementing...");
                    goto portloop;
                }

                Logger.Log.Error("General Error", ex);
                Environment.Exit(1);
            }

            ServerManager.Instance.ServerGroup = ConfigurationManager.AppSettings["ServerGroup"];
            var sessionLimit = Convert.ToInt32(ConfigurationManager.AppSettings["SessionLimit"]);
            int? newChannelId = CommunicationServiceClient.Instance.RegisterWorldServer(new SerializableWorldServer(ServerManager.Instance.WorldId, ip, port, sessionLimit, ServerManager.Instance.ServerGroup));

            if (newChannelId.HasValue)
            {
                ServerManager.Instance.ChannelId = newChannelId.Value;
                ServerManager.Instance.IpAddress = ip;
                ServerManager.Instance.Port = port;
                ServerManager.Instance.AccountLimit = sessionLimit;
                Console.Title = string.Format(Language.Instance.GetMessageFromKey("WORLD_SERVER_CONSOLE_TITLE"), ServerManager.Instance.ChannelId, ServerManager.Instance.Sessions.Count(), ServerManager.Instance.IpAddress, ServerManager.Instance.Port);
            }
            else
            {
                Logger.Log.ErrorFormat("Could not retrieve ChannelId from Web API.");
                Console.ReadKey();
            }
        }

        private static bool ExitHandler(CtrlType sig)
        {
            CommunicationServiceClient.Instance.UnregisterWorldServer(ServerManager.Instance.WorldId);

            ServerManager.Instance.Shout(string.Format(Language.Instance.GetMessageFromKey("SHUTDOWN_SEC"), 5));
            ServerManager.Instance.SaveAll();
            Thread.Sleep(5000);
            return false;
        }

        private static void RegisterMappings()
        {
            // register mappings for items
            DAOFactory.IteminstanceDAO.RegisterMapping(typeof(BoxInstance));
            DAOFactory.IteminstanceDAO.RegisterMapping(typeof(SpecialistInstance));
            DAOFactory.IteminstanceDAO.RegisterMapping(typeof(WearableInstance));
            DAOFactory.IteminstanceDAO.InitializeMapper(typeof(ItemInstance));

            // entities
            DAOFactory.AccountDAO.RegisterMapping(typeof(Account)).InitializeMapper();
            DAOFactory.EquipmentOptionDAO.RegisterMapping(typeof(EquipmentOptionDTO)).InitializeMapper();
            DAOFactory.CharacterDAO.RegisterMapping(typeof(Character)).InitializeMapper();
            DAOFactory.CharacterRelationDAO.RegisterMapping(typeof(CharacterRelationDTO)).InitializeMapper();
            DAOFactory.CharacterSkillDAO.RegisterMapping(typeof(CharacterSkill)).InitializeMapper();
            DAOFactory.ComboDAO.RegisterMapping(typeof(ComboDTO)).InitializeMapper();
            DAOFactory.DropDAO.RegisterMapping(typeof(DropDTO)).InitializeMapper();
            DAOFactory.GeneralLogDAO.RegisterMapping(typeof(GeneralLogDTO)).InitializeMapper();
            DAOFactory.ItemDAO.RegisterMapping(typeof(ItemDTO)).InitializeMapper();
            DAOFactory.BazaarItemDAO.RegisterMapping(typeof(BazaarItemDTO)).InitializeMapper();
            DAOFactory.MailDAO.RegisterMapping(typeof(MailDTO)).InitializeMapper();
            DAOFactory.MallDAO.RegisterMapping(typeof(MallDTO)).InitializeMapper();
            DAOFactory.RollGeneratedItemDAO.RegisterMapping(typeof(RollGeneratedItemDTO)).InitializeMapper();
            DAOFactory.MapDAO.RegisterMapping(typeof(MapDTO)).InitializeMapper();
            DAOFactory.MapMonsterDAO.RegisterMapping(typeof(MapMonster)).InitializeMapper();
            DAOFactory.MapNpcDAO.RegisterMapping(typeof(MapNpc)).InitializeMapper();
            DAOFactory.FamilyDAO.RegisterMapping(typeof(FamilyDTO)).InitializeMapper();
            DAOFactory.FamilyCharacterDAO.RegisterMapping(typeof(FamilyCharacterDTO)).InitializeMapper();
            DAOFactory.FamilyLogDAO.RegisterMapping(typeof(FamilyLogDTO)).InitializeMapper();
            DAOFactory.MapTypeDAO.RegisterMapping(typeof(MapTypeDTO)).InitializeMapper();
            DAOFactory.MapTypeMapDAO.RegisterMapping(typeof(MapTypeMapDTO)).InitializeMapper();
            DAOFactory.NpcMonsterDAO.RegisterMapping(typeof(NpcMonster)).InitializeMapper();
            DAOFactory.NpcMonsterSkillDAO.RegisterMapping(typeof(NpcMonsterSkill)).InitializeMapper();
            DAOFactory.PenaltyLogDAO.RegisterMapping(typeof(PenaltyLogDTO)).InitializeMapper();
            DAOFactory.PortalDAO.RegisterMapping(typeof(PortalDTO)).InitializeMapper();
            DAOFactory.PortalDAO.RegisterMapping(typeof(Portal)).InitializeMapper();
            DAOFactory.QuicklistEntryDAO.RegisterMapping(typeof(QuicklistEntryDTO)).InitializeMapper();
            DAOFactory.RecipeDAO.RegisterMapping(typeof(Recipe)).InitializeMapper();
            DAOFactory.RecipeItemDAO.RegisterMapping(typeof(RecipeItemDTO)).InitializeMapper();
            DAOFactory.MinilandObjectDAO.RegisterMapping(typeof(MinilandObjectDTO)).InitializeMapper();
            DAOFactory.MinilandObjectDAO.RegisterMapping(typeof(MapDesignObject)).InitializeMapper();
            DAOFactory.RespawnDAO.RegisterMapping(typeof(RespawnDTO)).InitializeMapper();
            DAOFactory.RespawnMapTypeDAO.RegisterMapping(typeof(RespawnMapTypeDTO)).InitializeMapper();
            DAOFactory.ShopDAO.RegisterMapping(typeof(Shop)).InitializeMapper();
            DAOFactory.ShopItemDAO.RegisterMapping(typeof(ShopItemDTO)).InitializeMapper();
            DAOFactory.ShopSkillDAO.RegisterMapping(typeof(ShopSkillDTO)).InitializeMapper();
            DAOFactory.CardDAO.RegisterMapping(typeof(CardDTO)).InitializeMapper();
            DAOFactory.BCardDAO.RegisterMapping(typeof(BCardDTO)).InitializeMapper();
            DAOFactory.CardDAO.RegisterMapping(typeof(Card)).InitializeMapper();
            DAOFactory.BCardDAO.RegisterMapping(typeof(BCard)).InitializeMapper();
            DAOFactory.SkillDAO.RegisterMapping(typeof(Skill)).InitializeMapper();
            DAOFactory.MateDAO.RegisterMapping(typeof(MateDTO)).InitializeMapper();
            DAOFactory.MateDAO.RegisterMapping(typeof(Mate)).InitializeMapper();
            DAOFactory.TeleporterDAO.RegisterMapping(typeof(TeleporterDTO)).InitializeMapper();
            DAOFactory.StaticBonusDAO.RegisterMapping(typeof(StaticBonusDTO)).InitializeMapper();
            DAOFactory.StaticBuffDAO.RegisterMapping(typeof(StaticBuffDTO)).InitializeMapper();
            DAOFactory.FamilyDAO.RegisterMapping(typeof(Family)).InitializeMapper();
            DAOFactory.FamilyCharacterDAO.RegisterMapping(typeof(FamilyCharacter)).InitializeMapper();
            DAOFactory.ScriptedInstanceDAO.RegisterMapping(typeof(ScriptedInstanceDTO)).InitializeMapper();
            DAOFactory.ScriptedInstanceDAO.RegisterMapping(typeof(ScriptedInstance)).InitializeMapper();
            DAOFactory.LogChatDAO.RegisterMapping(typeof(LogChatDTO)).InitializeMapper();
            DAOFactory.LogCommandsDAO.RegisterMapping(typeof(LogCommandsDTO)).InitializeMapper();
        }

        // TODO SEND MAIL TO REVIEW EXCEPTION
        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            ServerManager.Instance.InShutdown = true;
            Logger.Log.Error((Exception)e.ExceptionObject);
            Logger.Log.Debug(Language.Instance.GetMessageFromKey("SERVER_CRASH"));
            CommunicationServiceClient.Instance.UnregisterWorldServer(ServerManager.Instance.WorldId);

            ServerManager.Instance.Shout(string.Format(Language.Instance.GetMessageFromKey("SHUTDOWN_SEC"), 5));
            ServerManager.Instance.SaveAll();
            Thread.Sleep(5000);

            Process.Start(Process.GetCurrentProcess().ProcessName);
            Environment.Exit(1);
        }

        #endregion

        #region Classes

        public class NativeMethods
        {
            #region Methods

            [DllImport("Kernel32")]
            internal static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

            #endregion
        }

        #endregion
    }
}