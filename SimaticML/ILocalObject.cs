﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimaticML;

namespace SimaticML
{
    public interface ILocalObject
    {
        //LocalObjectData GetLocalObjectData();
        void UpdateLocalUId(IDGenerator localIDGeneration);
        void SetUId(uint uid);
        uint GetUId();
    }
}