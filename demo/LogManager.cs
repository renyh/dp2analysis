﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using log4net;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace demo
{
    public class LogManager
    {
        // public static ILog Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static ILog Logger = log4net.LogManager.GetLogger("Logging");
    }
}
