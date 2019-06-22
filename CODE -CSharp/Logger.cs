using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupraChargerWeb
{
    public static class Logger
    {
        public static string _erMsg = "Unknown error has occurred & has been sent to WebHost. ";

        public static void Log(Exception e, string msg = "")
        {
            if (msg != string.Empty) msg = $"Msg: {msg}\n\n";
            if (!Data._isLocal)
                Data.SendEmail("Error", msg + e.ToString(), Data._email);

            // Can't write file: Send email
            //string path = Data._otherFiles + "/Logger.txt";
            //System.IO.File.AppendAllText(path, msg + e.ToString() + "\r\r");
            //throw new Exception(e.ToString());
        }
    }
}