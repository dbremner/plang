﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;
using Microsoft.PSharp.Scheduling;

namespace ChordRacy
{
    /// <summary>
    /// This is an example of usign P#.
    /// 
    /// This example implements the Chord distributed lookup protocol from
    /// the [Chord: A Scalable Peer-to-peer Lookup Service for Internet
    /// Applications] SIGCOMM'01 paper.
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            new CommandLineOptions(args).Parse();

            if (Runtime.Options.Mode == Runtime.Mode.Execution)
            {
                Program.Run();
            }
            else if (Runtime.Options.Mode == Runtime.Mode.BugFinding)
            {
                TestConfiguration test = new TestConfiguration(
                    "ChordRacy",
                    Program.Run,
                    new RandomSchedulingStrategy(0),
                    100);

                //test.UntilBugFound = true;
                test.SoftTimeLimit = 600;
                Runtime.Test(test);
                Console.WriteLine(test.Result());
            }
        }

        public static void Run()
        {
            Console.WriteLine("Registering events to the runtime.\n");
            Runtime.RegisterNewEvent(typeof(eLocal));
            Runtime.RegisterNewEvent(typeof(eConfigure));
            Runtime.RegisterNewEvent(typeof(eJoin));
            Runtime.RegisterNewEvent(typeof(eJoinAck));
            Runtime.RegisterNewEvent(typeof(eFail));
            Runtime.RegisterNewEvent(typeof(eStop));
            Runtime.RegisterNewEvent(typeof(eStabilize));
            Runtime.RegisterNewEvent(typeof(eNotifySuccessor));
            Runtime.RegisterNewEvent(typeof(eAskForKeys));
            Runtime.RegisterNewEvent(typeof(eAskForKeysAck));
            Runtime.RegisterNewEvent(typeof(eFindSuccessor));
            Runtime.RegisterNewEvent(typeof(eFindSuccessorResp));
            Runtime.RegisterNewEvent(typeof(eFindPredecessor));
            Runtime.RegisterNewEvent(typeof(eFindPredecessorResp));
            Runtime.RegisterNewEvent(typeof(eQueryId));
            Runtime.RegisterNewEvent(typeof(eQueryIdResp));
            Runtime.RegisterNewEvent(typeof(eQueryJoin));
            Runtime.RegisterNewEvent(typeof(eNotifyClient));

            Console.WriteLine("Registering state machines to the runtime.\n");
            Runtime.RegisterNewMachine(typeof(Cluster));
            Runtime.RegisterNewMachine(typeof(ChordNode));
            Runtime.RegisterNewMachine(typeof(Client));

            Console.WriteLine("Starting the runtime.\n");
            Runtime.Start(new Tuple<int, List<int>, List<int>>(
                3,
                new List<int> { 0, 1, 3 },
                new List<int> { 1, 2, 6 }));
            Runtime.Wait();

            Console.WriteLine("Performing cleanup.\n");
            Runtime.Dispose();
        }
    }
}
