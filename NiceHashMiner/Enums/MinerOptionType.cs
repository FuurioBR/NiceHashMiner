﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NiceHashMiner.Enums {
    public enum MinerOptionType {
        NONE,
        // ccminer, sgminer
        Intensity,
        // sgminer
        KeccakUnroll,
        HamsiExpandBig,
        Nfactor,
        Xintensity,
        Rawintensity,
        ThreadConcurrency,
        Worksize,
        GpuThreads,
        LookupGap,
        RemoveDisabled,
        // sgminer temp
        GpuFan,
        TempCutoff,
        TempOverheat,
        TempTarget,
        AutoFan,
        AutoGpu,
        // ccminer cryptonight
        Ccminer_CryptoNightLaunch,
        Ccminer_CryptoNightBfactor,
        Ccminer_CryptoNightBsleep,
        // OCL ethminer
        Ethminer_OCL_LocalWork,
        Ethminer_OCL_GlobalWork,
        // CUDA ethminer
        CudaBlockSize,
        CudaGridSize,
        // TODO
        // cpuminer
        Threads,
        CpuAffinity,
        CpuPriority,
        // nheqminer/eqm
        // nheqminer CUDA
        CUDA_Solver_Version, // -cb
        CUDA_Solver_Block, // -cb
        CUDA_Solver_Thread, // -ct
        // nheqminer OpenCL
        OpenCL_Solver_Version, //-ov
        OpenCL_Solver_Dev_Thread, // -ot
        // eqm
        CUDA_Solver_Mode, // -cm
        // ClaymoreZcash
        ClaymoreZcash_i,  // -i
        ClaymoreZcash_wd, // -wd
        ClaymoreZcash_r,
        ClaymoreZcash_nofee,
        ClaymoreZcash_li,
        ClaymoreZcash_tt, // -tt
        ClaymoreZcash_ttli,
        ClaymoreZcash_tstop,
        ClaymoreZcash_fanmax,
        ClaymoreZcash_fanmin,
        ClaymoreZcash_cclock,
        ClaymoreZcash_mclock,
        ClaymoreZcash_powlim,
        ClaymoreZcash_cvddc,
        ClaymoreZcash_mvddc
    }
}
