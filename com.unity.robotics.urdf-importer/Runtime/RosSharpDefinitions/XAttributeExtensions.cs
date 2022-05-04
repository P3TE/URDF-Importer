/*
© Siemens AG, 2017
Author: Dr. Martin Bischoff (martin.bischoff@siemens.com)
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/  

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter
{
    public static class XAttributeExtensions
    {
        public static double[] ReadDoubleArray(this XAttribute attribute)
        {
            return Array.ConvertAll(
                ((string)attribute).Split(' ').Where(x => !string.IsNullOrEmpty(x)).ToArray(),
                i => Convert.ToDouble(i, CultureInfo.InvariantCulture));
        }

        public static double ReadOptionalDouble(this XAttribute attribute, double fallbackDefaultValue = Double.NaN)
        {
            return (attribute != null) ? (double)attribute : fallbackDefaultValue;
        }

        public static string DoubleArrayToString(this IEnumerable<double> arr)
        {
            string arrString = arr.Aggregate("", (current, num) => (current + " " + num));
            return arrString.Substring(1); //Gets rid of extra space at start of string
        }

        public static Color FloatArrayToColor(float[] data)
        {
            if (data.Length < 3)
            {
                throw new ArgumentException($"Colour data doesn't contain enough information, length = {data.Length}, should be 3 or 4");
            }
            if (data.Length > 5)
            {
                throw new ArgumentException($"Colour data contains too much information, length = {data.Length}, should be 3 or 4");
            }

            float r = data[0];
            float g = data[1];
            float b = data[2];
            float a = 1.0f;
            if (data.Length > 3)
            {
                a = data[3];
            }

            return new Color(r, g, b, a);
        }
    }
}
