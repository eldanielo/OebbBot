﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace oebbBot
{


    public class LUISModel
    {
        public string query { get; set; }
        public Intent[] intents { get; set; }
        public Entity[] entities { get; set; }
    }

    public class Intent
    {
        public string intent { get; set; }
        public float score { get; set; }
    }

    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public Resolution resolution { get; set; }
        public float score { get; set; }
    }

    public class Resolution
    {
        public string comment { get; set; }
        public string time { get; set; }
    }


}