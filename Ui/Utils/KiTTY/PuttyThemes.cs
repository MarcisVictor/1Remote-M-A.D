﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using Shawn.Utils.Wpf;

namespace _1RM.Utils.KiTTY
{
    public static class PuttyThemes
    {
        public static Dictionary<string, List<KittyConfigKeyValuePair>> GetThemes()
        {
            var uri = ResourceUriHelper.GetUriFromCurrentAssembly("Resources/KiTTY/PuttyThemes.json");
            var stream = Application.GetResourceStream(uri)?.Stream;
            Debug.Assert(stream != null);

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            var json = Encoding.UTF8.GetString(bytes);
            var themes = JsonConvert.DeserializeObject<Dictionary<string, List<KittyConfigKeyValuePair>>>(json);
            if (themes == null)
                throw new NullReferenceException("Resources/KiTTY/PuttyThemes.json can not be deserialize!");
            return themes;
        }
    }
}