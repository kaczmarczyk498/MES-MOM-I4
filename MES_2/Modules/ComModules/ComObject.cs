﻿//  ===============================
//  AUTHOR             : Honza-Jan Tichý
//  CREATE DATE     : 2017-01-29
//  ===============================
//  Namespace        : MES_application
//  Class                   : ComObject.cs
//  Description         :
//  ===============================
//  Version               :
//  Revision History : 2017-02-17
//  Change History: 
// ==================================
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using MES_2.Common;
using MES_application.Utils;
using Sharp7;

namespace MES_application.Modules.CommunicationModule
{
    public class ComObject : BaseModule, ILoggable
    {
        #region Values and Property
        private const string _VERSION = "0.0.1";
        public byte[] BufferData { get; set; }
        public byte[] WriteBufferData { get; set; }
        public DateTime NextRequestAt { get; set; }
        public ComObjectConfigure ObjectConfigure { get; set; }
        public int CountErrors { get; set; }
        public int LastError { get; private set; }
        public bool WriteSuccessful { get; set; } = true;

        public delegate void StateChangedEventHandler(ComObject o, ModuleStateChangedEventArgs e);

        public event StateChangedEventHandler StateChanged;

        #endregion

        #region Setup Object

        public ComObject(ComObjectConfigure p_configure)
        {
            ObjectConfigure = p_configure;
            BufferData = new byte[4];

            Configure();

            Run();
        }

        public void Configure()
        {
            CountErrors = 10;
            LastError = 0;
            NextRequestAt = DateTime.Now;
        }

        public override void Run()
        {
            EModuleState = BaseModuleEState.Running;
        }

        public override void Stop()
        {
            EModuleState = BaseModuleEState.Stopped;
        }

        public override void Restart()
        {
            base.Restart();
        }

        public override string Version()
        {
            return _VERSION;
        }

        #endregion

        #region ReadingWrite

        public ReceivedResult ReadWriteCycle(S7Client p_objClient)
        {
            if (EModuleState != BaseModuleEState.Stopped)
            {
                switch (ObjectConfigure._ERW)
                {
                    case ComObjectConfigure.ETypeComObject.Read:
                        return Readdata(p_objClient);
                    case ComObjectConfigure.ETypeComObject.Write:
                        Writedata(p_objClient);
                        return null;
                    case ComObjectConfigure.ETypeComObject.ReadWrite:
                        Writedata(p_objClient);
                        return Readdata(p_objClient);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return null;
        }

        public ReceivedResult Readdata(S7Client p_objClient)
        {
            // DateTime dytNow = NextRequestAt;
            if (NextRequestAt <= DateTime.Now)
            {
                ReceivedResult Result = new ReceivedResult();

                Result.ErrorNumber = p_objClient.ReadArea(ObjectConfigure.AreaOfMemory, ObjectConfigure.DbNumber,
                    ObjectConfigure.StartOffset, 1, ObjectConfigure.WorldLen, BufferData);

                ShowResult(Result.ErrorNumber, p_objClient); // debug

                if (Result.ErrorNumber == 0)
                {
                    NextRequestAt = NextRequestAt.AddMilliseconds(ObjectConfigure.PeriodOfCheck);
                    Result.Data = GetValueOfTypeFromPlc();
                    Result.IdComObj = ObjectConfigure.Id;
                    return Result;
                }

                ErrorManager(Result.ErrorNumber); // chyba v komunikaci/CPU nebo jiná
            }

            return null; // ShowResult(LastError, p_objClient); // debug - > null
        }

        public void Writedata(S7Client p_objClient)
        {
            if (WriteSuccessful == false)
            {
                
                var  iResult = p_objClient.WriteArea(ObjectConfigure.AreaOfMemory, ObjectConfigure.DbNumber,
                    ObjectConfigure.StartOffset, 1, ObjectConfigure.WorldLen, WriteBufferData );

                if (iResult == 0)
                {
                    WriteSuccessful = true;
                    return;
                }
                ErrorManager(iResult);
            }
        }

        private int GetValueOfTypeFromPlc()
        {
            switch (ObjectConfigure.WorldLen)
            {
                case S7Consts.S7WLBit:
                    return Convert.ToInt32(S7.GetBitAt(BufferData, 0, 0));
                case S7Consts.S7WLByte:
                case S7Consts.S7WLChar:
                    return Convert.ToInt32(S7.GetByteAt(BufferData, 0));
                case S7Consts.S7WLWord:
                case S7Consts.S7WLInt:
                    return Convert.ToInt32(S7.GetIntAt(BufferData, 0));
                case S7Consts.S7WLDWord:
                case S7Consts.S7WLReal:
                    return Convert.ToInt32(S7.GetRealAt(BufferData, 0));
                default:
                    return 0;
            }
        }

        #endregion

        #region Servicies

        private void ErrorManager(int p_nowError)
        {
            CountErrors--; // pocetchyb
            EModuleState = BaseModuleEState.Problem;
            LastError = p_nowError;

            if (CountErrors == 0)
                ReportProblem();
        }

        private void ReportProblem()
        {
            EModuleState = BaseModuleEState.Stopped;
            StateChangeEvent();
        }

        private void ShowResult(int Result, S7Client p_objClient)
        {
            // This function returns a textual explaination of the error code
            if (Result == 0)
                Console.WriteLine(p_objClient.ErrorText(Result) + " (" + p_objClient.ExecutionTime.ToString() +
                                  " ms)");
            else
                Console.WriteLine(p_objClient.ErrorText(Result));
        }

        private void StateChangeEvent()
        {
            if (StateChanged != null)
            {
                var args = new ModuleStateChangedEventArgs()
                {
                    EStateNOW = BaseModuleEState.Stopped,
                    EStatePrev = BaseModuleEState.Stopped,
                    StateDescription = "AKTIVNI"
                };
                StateChanged(this, args);
            }
        }

        public override bool Validate()
        {
            return false;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return base.ToString();
        }

        public string Log()
        {
            return null;
        }

        #endregion
    }
}