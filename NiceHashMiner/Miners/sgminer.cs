﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Management;
using NiceHashMiner.Configs;
using NiceHashMiner.Devices;
using NiceHashMiner.Enums;
using NiceHashMiner.Miners.Grouping;
using NiceHashMiner.Miners.Parsing;
using System.Threading;

namespace NiceHashMiner.Miners
{
    class sgminer : Miner
    {
        private readonly int GPUPlatformNumber;
        // benchmark helper variables
        bool _benchmarkOnce = true;
        Stopwatch _benchmarkTimer = new Stopwatch();

        public sgminer()
            : base("sgminer_AMD")
        {            
            Path = MinerPaths.sgminer_5_5_0_general;
            GPUPlatformNumber = ComputeDeviceManager.Avaliable.AMDOpenCLPlatformNum;
            IsKillAllUsedMinerProcs = true;
        }

        // use ONLY for exiting a benchmark
        public void KillSGMiner() {
            foreach (Process process in Process.GetProcessesByName("sgminer")) {
                try { process.Kill(); } catch (Exception e) { Helpers.ConsolePrint(MinerDeviceName, e.ToString()); }
            }
        }

        public override void EndBenchmarkProcces() {
            if (BenchmarkProcessStatus != BenchmarkProcessStatus.Killing && BenchmarkProcessStatus != BenchmarkProcessStatus.DoneKilling) {
                BenchmarkProcessStatus = BenchmarkProcessStatus.Killing;
                try {
                    Helpers.ConsolePrint("BENCHMARK", String.Format("Trying to kill benchmark process {0} algorithm {1}", BenchmarkProcessPath, BenchmarkAlgorithm.GetName()));
                    KillSGMiner();
                } catch { } finally {
                    BenchmarkProcessStatus = BenchmarkProcessStatus.DoneKilling;
                    Helpers.ConsolePrint("BENCHMARK", String.Format("Benchmark process {0} algorithm {1} KILLED", BenchmarkProcessPath, BenchmarkAlgorithm.GetName()));
                    //BenchmarkHandle = null;
                }
            }
        }

        protected override int GET_MAX_CooldownTimeInMilliseconds() {
            return 90 * 1000; // 1.5 minute max, whole waiting time 75seconds
        }

        protected override void _Stop(MinerStopType willswitch) {
            Stop_cpu_ccminer_sgminer_nheqminer(willswitch);
        }

        public override void Start(string url, string btcAdress, string worker)
        {
            if (!IsInit) {
                Helpers.ConsolePrint(MinerTAG(), "MiningSetup is not initialized exiting Start()");
                return;
            }
            string username = GetUsername(btcAdress, worker);

            Path = MiningSetup.MinerPath;
            WorkingDirectory = Path.Replace("sgminer.exe", "");

            LastCommandLine = " --gpu-platform " + GPUPlatformNumber +
                              " -k " + MiningSetup.MinerName +
                              " --url=" + url +
                              " --userpass=" + username + ":" + Globals.PasswordDefault +
                              " --api-listen" +
                              " --api-port=" + APIPort.ToString() +
                              " " +
                              ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.AMD) +
                              " --device ";

            LastCommandLine += GetDevicesCommandString();

            ProcessHandle = _Start();
        }

        protected override bool UpdateBindPortCommand(int oldPort, int newPort) {
            // --api-port=
            const string MASK = "--api-port={0}";
            var oldApiBindStr = String.Format(MASK, oldPort);
            var newApiBindStr = String.Format(MASK, newPort);
            if (LastCommandLine.Contains(oldApiBindStr)) {
                LastCommandLine = LastCommandLine.Replace(oldApiBindStr, newApiBindStr);
                return true;
            }
            return false;
        }

        // new decoupled benchmarking routines
        #region Decoupled benchmarking routines

        protected override string BenchmarkCreateCommandLine(Algorithm algorithm, int time) {
            string CommandLine;
            Path = "cmd";
            string MinerPath = MiningSetup.MinerPath;

            var nhAlgorithmData = Globals.NiceHashData[algorithm.NiceHashID];
            string url = "stratum+tcp://" + nhAlgorithmData.name + "." +
                         Globals.MiningLocation[ConfigManager.GeneralConfig.ServiceLocation] + ".nicehash.com:" +
                         nhAlgorithmData.port;

            // demo for benchmark
            string username = Globals.DemoUser;

            if (ConfigManager.GeneralConfig.WorkerName.Length > 0)
                username += "." + ConfigManager.GeneralConfig.WorkerName.Trim();

            // cd to the cgminer for the process bins
            CommandLine = " /C \"cd /d " + MinerPath.Replace("sgminer.exe", "") + " && sgminer.exe " +
                          " --gpu-platform " + GPUPlatformNumber +
                          " -k " + algorithm.MinerName +
                          " --url=" + url +
                          " --userpass=" + username + ":" + Globals.PasswordDefault +
                          " --sched-stop " + DateTime.Now.AddSeconds(time).ToString("HH:mm") +
                          " -T --log 10 --log-file dump.txt" +
                          " " +
                          ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.AMD) +
                          " --device ";

            CommandLine += GetDevicesCommandString();

            CommandLine += " && del dump.txt\"";

            return CommandLine;
        }

        protected override bool BenchmarkParseLine(string outdata) {
            if (outdata.Contains("Average hashrate:") && outdata.Contains("/s")) {
                int i = outdata.IndexOf(": ");
                int k = outdata.IndexOf("/s");

                // save speed
                string hashSpeed = outdata.Substring(i + 2, k - i + 2);
                Helpers.ConsolePrint("BENCHMARK", "Final Speed: " + hashSpeed);

                hashSpeed = hashSpeed.Substring(0, hashSpeed.IndexOf(" "));
                double speed = Double.Parse(hashSpeed, CultureInfo.InvariantCulture);

                if (outdata.Contains("Kilohash"))
                    speed *= 1000;
                else if (outdata.Contains("Megahash"))
                    speed *= 1000000;

                BenchmarkAlgorithm.BenchmarkSpeed = speed;
                return true;
            }
            return false;
        }

        protected override void BenchmarkThreadRoutineStartSettup() {
            // sgminer extra settings
            AlgorithmType NHDataIndex = BenchmarkAlgorithm.NiceHashID;

            if (Globals.NiceHashData == null) {
                Helpers.ConsolePrint("BENCHMARK", "Skipping sgminer benchmark because there is no internet " +
                    "connection. Sgminer needs internet connection to do benchmarking.");

                throw new Exception("No internet connection");
            }

            if (Globals.NiceHashData[NHDataIndex].paying == 0) {
                Helpers.ConsolePrint("BENCHMARK", "Skipping sgminer benchmark because there is no work on Nicehash.com " +
                    "[algo: " + BenchmarkAlgorithm.GetName() + "(" + NHDataIndex + ")]");

                throw new Exception("No work can be used for benchmarking");
            }

            _benchmarkTimer.Reset();
            _benchmarkTimer.Start();
            // call base, read only outpus
            //BenchmarkHandle.BeginOutputReadLine();
        }

        protected override void BenchmarkOutputErrorDataReceivedImpl(string outdata) {
            if (_benchmarkTimer.Elapsed.Minutes >= BenchmarkTimeInSeconds + 1 && _benchmarkOnce == true) {
                _benchmarkOnce = false;
                string resp = GetAPIData(APIPort, "quit").TrimEnd(new char[] { (char)0 });
                Helpers.ConsolePrint("BENCHMARK", "SGMiner Response: " + resp);
            }
            if (_benchmarkTimer.Elapsed.Minutes >= BenchmarkTimeInSeconds + 2) {
                _benchmarkTimer.Stop();
                // this is safe in a benchmark
                KillSGMiner();
                BenchmarkSignalHanged = true;
            }
            if (!BenchmarkSignalFinnished && outdata != null) {
                CheckOutdata(outdata);
            }
        }

        protected override void BenchmarkThreadRoutine(object CommandLine) {
            Thread.Sleep(ConfigManager.GeneralConfig.MinerRestartDelayMS);

            BenchmarkSignalQuit = false;
            BenchmarkSignalHanged = false;
            BenchmarkSignalFinnished = false;
            BenchmarkException = null;

            try {
                Helpers.ConsolePrint("BENCHMARK", "Benchmark starts");
                BenchmarkHandle = BenchmarkStartProcess((string)CommandLine);
                BenchmarkThreadRoutineStartSettup();
                // wait a little longer then the benchmark routine if exit false throw
                //var timeoutTime = BenchmarkTimeoutInSeconds(BenchmarkTimeInSeconds);
                //var exitSucces = BenchmarkHandle.WaitForExit(timeoutTime * 1000);
                // don't use wait for it breaks everything
                BenchmarkProcessStatus = BenchmarkProcessStatus.Running;
                while(true) {
                    string outdata = BenchmarkHandle.StandardOutput.ReadLine();
                    BenchmarkOutputErrorDataReceivedImpl(outdata);
                    // terminate process situations
                    if (BenchmarkSignalQuit
                        || BenchmarkSignalFinnished
                        || BenchmarkSignalHanged
                        || BenchmarkSignalTimedout
                        || BenchmarkException != null) {
                        //EndBenchmarkProcces();
                        // this is safe in a benchmark
                        KillSGMiner();
                        if (BenchmarkSignalTimedout) {
                            throw new Exception("Benchmark timedout");
                        }
                        if (BenchmarkException != null) {
                            throw BenchmarkException;
                        }
                        if (BenchmarkSignalQuit) {
                            throw new Exception("Termined by user request");
                        }
                        if (BenchmarkSignalHanged) {
                            throw new Exception("SGMiner is not responding");
                        }
                        if (BenchmarkSignalFinnished) {
                            break;
                        }
                    }
                }
            } catch (Exception ex) {
                BenchmarkAlgorithm.BenchmarkSpeed = 0;

                Helpers.ConsolePrint(MinerTAG(), "Benchmark Exception: " + ex.Message);
                if (BenchmarkComunicator != null && !OnBenchmarkCompleteCalled) {
                    OnBenchmarkCompleteCalled = true;
                    BenchmarkComunicator.OnBenchmarkComplete(false, BenchmarkSignalTimedout ? International.GetText("Benchmark_Timedout") : International.GetText("Benchmark_Terminated"));
                }
            } finally {
                BenchmarkProcessStatus = BenchmarkProcessStatus.Success;
                Helpers.ConsolePrint("BENCHMARK", "Final Speed: " + Helpers.FormatSpeedOutput(BenchmarkAlgorithm.BenchmarkSpeed));
                Helpers.ConsolePrint("BENCHMARK", "Benchmark ends");
                if (BenchmarkComunicator != null && !OnBenchmarkCompleteCalled) {
                    OnBenchmarkCompleteCalled = true;
                    BenchmarkComunicator.OnBenchmarkComplete(true, "Success");
                }
            }
        }

        #endregion // Decoupled benchmarking routines

        // TODO _currentMinerReadStatus
        public override APIData GetSummary() {
            string resp;
            APIData ad = new APIData(MiningSetup.CurrentAlgorithmType);

            resp = GetAPIData(APIPort, "summary");
            if (resp == null) {
                _currentMinerReadStatus = MinerAPIReadStatus.NONE;
                return null;
            }

            try {
                // Checks if all the GPUs are Alive first
                string resp2 = GetAPIData(APIPort, "devs");
                if (resp2 == null) {
                    _currentMinerReadStatus = MinerAPIReadStatus.NONE;
                    return null;
                }

                string[] checkGPUStatus = resp2.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 1; i < checkGPUStatus.Length - 1; i++) {
                    if (!checkGPUStatus[i].Contains("Status=Alive")) {
                        Helpers.ConsolePrint(MinerTAG(), ProcessTag() + " GPU " + i + ": Sick/Dead/NoStart/Initialising/Disabled/Rejecting/Unknown");
                        _currentMinerReadStatus = MinerAPIReadStatus.WAIT;
                        return null;
                    }
                }

                string[] resps = resp.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                if (resps[1].Contains("SUMMARY")) {
                    string[] data = resps[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    // Get miner's current total speed
                    string[] speed = data[4].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    // Get miner's current total MH
                    double total_mh = Double.Parse(data[18].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1], new CultureInfo("en-US"));

                    ad.Speed = Double.Parse(speed[1]) * 1000;

                    if (total_mh <= PreviousTotalMH) {
                        Helpers.ConsolePrint(MinerTAG(), ProcessTag() + " SGMiner might be stuck as no new hashes are being produced");
                        Helpers.ConsolePrint(MinerTAG(), ProcessTag() + " Prev Total MH: " + PreviousTotalMH + " .. Current Total MH: " + total_mh);
                        _currentMinerReadStatus = MinerAPIReadStatus.NONE;
                        return null;
                    }

                    PreviousTotalMH = total_mh;
                } else {
                    ad.Speed = 0;
                }
            } catch {
                _currentMinerReadStatus = MinerAPIReadStatus.NONE;
                return null;
            }

            _currentMinerReadStatus = MinerAPIReadStatus.GOT_READ;
            // check if speed zero
            if (ad.Speed == 0) _currentMinerReadStatus = MinerAPIReadStatus.READ_SPEED_ZERO;

            return ad;
        }
    }
}
