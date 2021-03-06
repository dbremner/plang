﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Formula.API;
using Microsoft.Formula.API.ASTQueries;
using Microsoft.Formula.API.Nodes;
using Microsoft.Formula.Common;

namespace Microsoft.Pc
{
    internal class ZingTranslationInfo
    {
        public AST<Node> node = null;

        public ZingTranslationInfo(AST<Node> n)
        {
            this.node = n;
        }
    }

    internal class TransitionInfo
    {
        public string target;
        public string transFunName;

        public TransitionInfo(string target)
        {
            this.target = target;
            this.transFunName = null;
        }

        public TransitionInfo(string target, string transFunName)
        {
            this.target = target;
            this.transFunName = transFunName;
        }

        public bool IsPush { get { return transFunName == null; } }
    }

    enum StateTemperature { COLD, WARM, HOT }

    internal class StateInfo
    {
        public string ownerName;
        public string entryActionName;
        public string exitFunName;
        public bool hasNullTransition;
        public Dictionary<string, TransitionInfo> transitions;
        public Dictionary<string, string> actions;
        public List<string> deferredEvents;
        public List<string> ignoredEvents;
        public StateTemperature temperature;
        public string printedName;

        public bool IsHot
        {
            get { return temperature == StateTemperature.HOT; }
        }

        public bool IsCold
        {
            get { return temperature == StateTemperature.COLD; }
        }

        public bool IsWarm
        {
            get { return temperature == StateTemperature.WARM; }
        }

        public StateInfo(string ownerName, string entryActionName, string exitFunName, StateTemperature temperature, string printedName)
        {
            this.ownerName = ownerName;
            this.entryActionName = entryActionName;
            this.exitFunName = exitFunName;
            this.hasNullTransition = false;
            this.transitions = new Dictionary<string, TransitionInfo>();
            this.actions = new Dictionary<string, string>();
            this.deferredEvents = new List<string>();
            this.ignoredEvents = new List<string>();
            this.temperature = temperature;
            this.printedName = printedName;
        }
    }

    internal class VariableInfo
    {
        public FuncTerm type;

        public VariableInfo(FuncTerm type)
        {
            this.type = type;
        }
    }

    internal class LocalVariableInfo : VariableInfo
    {
        public int index;

        public LocalVariableInfo(FuncTerm type, int index) : base(type)
        {
            this.index = index;
        }
    }

    internal class FunInfo
    {
        public bool isModel;
        public AST<FuncTerm> returnType;
        public Node body;
        public Dictionary<string, LocalVariableInfo> parameterNameToInfo;
        public List<string> parameterNames;
        public int numFairChoices;
        public bool isAnonymous;
        public Dictionary<AST<Node>, FuncTerm> typeInfo;
        public HashSet<Node> invokeSchedulerFuns;


        public FunInfo(bool isModel, AST<FuncTerm> returnType, Node body)
        {
            this.isModel = isModel;
            this.returnType = returnType;
            this.body = body;
            this.parameterNameToInfo = new Dictionary<string, LocalVariableInfo>();
            this.parameterNames = new List<string>();
            this.numFairChoices = 0;
            this.isAnonymous = false;
            this.typeInfo = new Dictionary<AST<Node>, FuncTerm>();
            this.invokeSchedulerFuns = new HashSet<Node>();
        }

        public FunInfo(Node body, bool isAnonymous)
        {
            this.isModel = false;
            this.returnType = PToZing.PTypeNull;
            this.body = body;
            this.parameterNameToInfo = new Dictionary<string, LocalVariableInfo>();
            this.parameterNames = new List<string>();
            this.numFairChoices = 0;
            this.isAnonymous = isAnonymous;
            this.typeInfo = new Dictionary<AST<Node>, FuncTerm>();
            this.invokeSchedulerFuns = new HashSet<Node>();
        }
    }

    enum MonitorType { SAFETY, FINALLY, REPEATEDLY };

    internal class MachineInfo
    {
        public bool IsReal { get { return type == "REAL"; } }
        public bool IsModel { get { return type == "MODEL"; } }
        public bool IsMonitor { get { return type == "MONITOR"; } }

        public string type;
        public int maxQueueSize;
        public bool maxQueueSizeAssumed;
        public string initStateName;
        public Dictionary<string, StateInfo> stateNameToStateInfo;
        public Dictionary<string, VariableInfo> localVariableToVarInfo;
        public Dictionary<string, List<string>> eventSetNameToEvents;
        public Dictionary<string, FunInfo> funNameToFunInfo;
        public MonitorType monitorType;

        public MachineInfo()
        {
            type = "REAL";
            maxQueueSize = -1;
            maxQueueSizeAssumed = false;
            initStateName = null;
            stateNameToStateInfo = new Dictionary<string, StateInfo>();
            localVariableToVarInfo = new Dictionary<string, VariableInfo>();
            eventSetNameToEvents = new Dictionary<string, List<string>>();
            funNameToFunInfo = new Dictionary<string, FunInfo>();
            monitorType = MonitorType.SAFETY;
            funNameToFunInfo["ignore"] = new FunInfo(Factory.Instance.AddArg(Factory.Instance.MkFuncTerm(PData.Con_NulStmt), PData.Cnst_Skip).Node, false);
        }
    }

    internal class EventInfo
    {
        public int maxInstances;  // -1 represents no bound
        public bool maxInstancesAssumed;
        public FuncTerm payloadType;

        public EventInfo(FuncTerm payloadType)
        {
            this.payloadType = payloadType;
            this.maxInstances = -1;
        }

        public EventInfo(int maxInstances, bool maxInstancesAssumed, FuncTerm payloadType)
        {
            this.maxInstances = maxInstances;
            this.maxInstancesAssumed = maxInstancesAssumed;
            this.payloadType = payloadType;
        }
    }

    class PToZing
    {
        public static AST<FuncTerm> PTypeNull = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("NULL"));
        public static AST<FuncTerm> PTypeBool = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("BOOL"));
        public static AST<FuncTerm> PTypeInt = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("INT"));
        public static AST<FuncTerm> PTypeEvent = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("EVENT"));
        public static AST<FuncTerm> PTypeReal = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("REAL"));
        public static AST<FuncTerm> PTypeAny = AddArgs(Factory.Instance.MkFuncTerm(Factory.Instance.MkId("BaseType")), Factory.Instance.MkId("ANY"));

        public const string SM_EVENT = "SM_EVENT";
        public static AST<Node> SmEvent = Factory.Instance.MkCnst("SM_EVENT");
        public const string SM_TRANSITION = "SM_TRANSITION";
        public static AST<Node> SmTransition = Factory.Instance.MkCnst("SM_TRANSITION");
        public const string SM_STATE = "SM_STATE";
        public static AST<Node> SmState = Factory.Instance.MkCnst("SM_STATE");
        public const string SM_HANDLE = "SM_HANDLE";
        public static AST<Node> SmHandle = Factory.Instance.MkCnst("SM_HANDLE");
        public static string SM_EVENT_SET = "SM_EVENT_SET";
        public static AST<Node> SmEventSet = Factory.Instance.MkCnst("SM_EVENT_SET");
        public const string PRT_VALUE = "PRT_VALUE";
        public static AST<Node> PrtValue = Factory.Instance.MkCnst("PRT_VALUE");
        public static AST<Node> PrtCastValue = MkZingDot("PRT_VALUE", "PrtCastValue");
        public static AST<Node> PrtMkDefaultValue = MkZingDot("PRT_VALUE", "PrtMkDefaultValue");
        public static AST<Node> PrtCloneValue = MkZingDot("PRT_VALUE", "PrtCloneValue");
        public static AST<Node> PrtIsEqualValue = MkZingDot("PRT_VALUE", "PrtIsEqualValue");

        public const string NullEvent = "null";
        public const string HaltEvent = "halt";

        private static HashSet<string> ReservedNames = new HashSet<string>(
            new string[] { "Main", "SM_NULL", "MachineId", "SM_EVENT", "SM_TRANSITION", "SM_STATE", "SM_HANDLE", "EVENT_NODE", "EVENT_BUFFER", "StateStack", "StackFrame", "Continuation", 
                           "FairScheduler", "FairChoice", "FairCycle", "PRT_FIELD_NAME", "PRT_TYPE", "PRT_VALUE", "PRT_SEQ", "PRT_MAP",
                           "Machine", "Event", "State", "ActionOrFun", "ContinuationReason", "GateStatus", "StateTemperature", "PRT_TYPE_KIND", 
                            "SM_EVENT_ARRAY", "SM_TRANSITION_ARRAY", "ActionOrFun_ARRAY", "PRT_TYPE_ARRAY", "PRT_FIELD_NAME_ARRAY", "PRT_VALUE_ARRAY" });

        private static string ZingMachineClassName(string machineName)
        {
            return string.Format("MACHINE_{0}", machineName);
        }

        private static string ZingMachineClassSetTypeName(string machineName)
        {
            return string.Format("SET_{0}", machineName);
        }

        public Dictionary<string, EventInfo> allEvents;
        public Dictionary<string, MachineInfo> allMachines;
        public Dictionary<string, FunInfo> allStaticFuns;
        public string mainMachineName;
        private Dictionary<AST<Node>, string> anonFunToName;

        public LinkedList<AST<FuncTerm>> GetBin(Dictionary<string, LinkedList<AST<FuncTerm>>> factBins, FuncTerm ft)
        {
            var fun = (Id)ft.Function;
            return GetBin(factBins, fun.Name);
        }

        public LinkedList<AST<FuncTerm>> GetBin(Dictionary<string, LinkedList<AST<FuncTerm>>> factBins, string name)
        {
            Contract.Requires(!string.IsNullOrEmpty(name));
            LinkedList<AST<FuncTerm>> bin;
            if (!factBins.TryGetValue(name, out bin))
            {
                bin = new LinkedList<AST<FuncTerm>>();
                factBins.Add(name, bin);
            }

            return bin;
        }

        public string GetMachineName(FuncTerm ft, int index)
        {
            FuncTerm machineDecl = (FuncTerm)GetArgByIndex(ft, index);
            var machineName = GetName(machineDecl, 0);
            return machineName;
        }

        Dictionary<string, int> uniqIDCounters = new Dictionary<string, int>();
        public string GetUnique(string prefix)
        {
            if (!uniqIDCounters.ContainsKey(prefix))
                uniqIDCounters[prefix] = 0;

            var ret = uniqIDCounters[prefix];
            uniqIDCounters[prefix]++;
            return prefix + '_' + ret;
        }

        Compiler compiler;
        public PToZing(Compiler compiler, List<AST<Model>> allModels, AST<Model> modelWithTypes)
        {
            this.compiler = compiler;
            this.typeContext = new TypeTranslationContext();
            GenerateProgramData(allModels);
            GenerateTypeInfo(modelWithTypes);
        }

        private void GenerateProgramData(List<AST<Model>> allModels)
        {
            var factBins = new Dictionary<string, LinkedList<AST<FuncTerm>>>();

            foreach (var model in allModels)
            {
                model.FindAll(
                    new NodePred[]
                    {
                        NodePredFactory.Instance.Star,
                        NodePredFactory.Instance.MkPredicate(NodeKind.ModelFact)
                    },
                    (path, n) =>
                    {
                        var mf = (ModelFact)n;
                        FuncTerm ft = (FuncTerm)mf.Match;
                        GetBin(factBins, ft).AddLast((AST<FuncTerm>)Factory.Instance.ToAST(ft));
                    });
            }

            allEvents = new Dictionary<string, EventInfo>();
            allEvents[HaltEvent] = new EventInfo(1, false, PTypeNull.Node);
            allEvents[NullEvent] = new EventInfo(1, false, PTypeNull.Node);
            allMachines = new Dictionary<string, MachineInfo>();
            allStaticFuns = new Dictionary<string, FunInfo>();

            LinkedList<AST<FuncTerm>> terms;

            terms = GetBin(factBins, "EventDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var name = ((Cnst)it.Current).GetStringValue();
                    it.MoveNext();
                    var bound = it.Current;
                    it.MoveNext();
                    var payloadType = (FuncTerm)(it.Current.NodeKind == NodeKind.Id ? PTypeNull.Node : it.Current);
                    if (bound.NodeKind == NodeKind.Id)
                    {
                        allEvents[name] = new EventInfo(payloadType);
                    }
                    else
                    {
                        var ft = (FuncTerm)bound;
                        var maxInstances = (int)((Cnst)GetArgByIndex(ft, 0)).GetNumericValue().Numerator;
                        var maxInstancesAssumed = ((Id)ft.Function).Name == "AssumeMaxInstances";
                        allEvents[name] = new EventInfo(maxInstances, maxInstancesAssumed, payloadType);
                    }
                }
            }

            terms = GetBin(factBins, "MachineDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var machineName = ((Cnst)it.Current).GetStringValue();
                    allMachines[machineName] = new MachineInfo();
                    it.MoveNext();
                    allMachines[machineName].type = ((Id)it.Current).Name;
                    it.MoveNext();
                    var bound = it.Current;
                    if (bound.NodeKind != NodeKind.Id)
                    {
                        var ft = (FuncTerm)bound;
                        allMachines[machineName].maxQueueSize = (int)((Cnst)GetArgByIndex(ft, 0)).GetNumericValue().Numerator;
                        allMachines[machineName].maxQueueSizeAssumed = ((Id)ft.Function).Name == "AssumeMaxInstances";
                    }
                    it.MoveNext();
                    allMachines[machineName].initStateName = GetNameFromQualifiedName(machineName, (FuncTerm)it.Current);
                    it.MoveNext();
                    if (((Id)it.Current).Name == "TRUE")
                    {
                        mainMachineName = machineName;
                    }
                }
            }

            terms = GetBin(factBins, "VarDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var varName = ((Cnst)it.Current).GetStringValue();
                    it.MoveNext();
                    var machineDecl = (FuncTerm)it.Current;
                    var machineName = GetName(machineDecl, 0);
                    var varTable = allMachines[machineName].localVariableToVarInfo;
                    it.MoveNext();
                    var type = (FuncTerm)it.Current;
                    varTable[varName] = new VariableInfo(type);
                }
            }

            terms = GetBin(factBins, "FunDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    string funName = ((Cnst)it.Current).GetStringValue();
                    it.MoveNext();
                    var owner = it.Current;
                    it.MoveNext();
                    var isModel = ((Id)it.Current).Name == "MODEL";
                    it.MoveNext();
                    var iter = it.Current as FuncTerm;
                    it.MoveNext();
                    var returnTypeName = it.Current is Id ? PTypeNull : (AST<FuncTerm>)Factory.Instance.ToAST(it.Current);
                    it.MoveNext();
                    var funInfo = new FunInfo(isModel, returnTypeName, it.Current);
                    int index = 0;
                    while (iter != null)
                    {
                        var ft = (FuncTerm)GetArgByIndex(iter, 0);
                        using (var enumerator = ft.Args.GetEnumerator())
                        {
                            enumerator.MoveNext();
                            var varName = ((Cnst)enumerator.Current).GetStringValue();
                            enumerator.MoveNext();
                            var varType = (FuncTerm)enumerator.Current;
                            funInfo.parameterNameToInfo[varName] = new LocalVariableInfo(varType, index);
                            funInfo.parameterNames.Add(varName);
                        }
                        iter = GetArgByIndex(iter, 1) as FuncTerm;
                        index++;
                    }
                    if (owner is FuncTerm)
                    {
                        var machineDecl = (FuncTerm)owner;
                        var machineName = GetName(machineDecl, 0);
                        var machineInfo = allMachines[machineName];
                        machineInfo.funNameToFunInfo[funName] = funInfo;
                    } 
                    else
                    {
                        allStaticFuns[funName] = funInfo;
                    }
                }
            }

            this.anonFunToName = new Dictionary<AST<Node>, string>();
            var anonFunCounter = new Dictionary<string, int>();
            foreach (var x in allMachines.Keys)
            {
                anonFunCounter[x] = 0;
            }
            terms = GetBin(factBins, "AnonFunDecl");
            foreach (var term in terms)
            {
                if (anonFunToName.ContainsKey(term)) continue;
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var machineDecl = (FuncTerm)it.Current;
                    var machineName = GetName(machineDecl, 0);
                    var machineInfo = allMachines[machineName];
                    it.MoveNext();
                    var funName = "AnonFun" + anonFunCounter[machineName];
                    machineInfo.funNameToFunInfo[funName] = new FunInfo(it.Current, true);
                    anonFunToName[term] = funName;
                    anonFunCounter[machineName]++;
                }
            }

            terms = GetBin(factBins, "StateDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var qualifiedStateName = (FuncTerm)it.Current;
                    it.MoveNext();
                    var machineDecl = (FuncTerm)it.Current;
                    var ownerName = GetName(machineDecl, 0);
                    var stateName = GetNameFromQualifiedName(ownerName, qualifiedStateName);
                    it.MoveNext();
                    var entryActionName = it.Current.NodeKind == NodeKind.Cnst
                                            ? ((Cnst)it.Current).GetStringValue()
                                            : anonFunToName[Factory.Instance.ToAST(it.Current)];
                    it.MoveNext();
                    var exitFunName = it.Current.NodeKind == NodeKind.Cnst
                                            ? ((Cnst)it.Current).GetStringValue()
                                            : anonFunToName[Factory.Instance.ToAST(it.Current)];
                    it.MoveNext();
                    var temperature = StateTemperature.WARM;
                    var t = ((Id)it.Current).Name;
                    if (t == "HOT")
                    {
                        temperature = StateTemperature.HOT;
                    }
                    else if (t == "COLD")
                    {
                        temperature = StateTemperature.COLD;
                    }
                    var stateTable = allMachines[ownerName].stateNameToStateInfo;
                    stateTable[stateName] = new StateInfo(ownerName, entryActionName, exitFunName, temperature, GetPrintedNameFromQualifiedName(qualifiedStateName));
                }
            }

            terms = GetBin(factBins, "TransDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var stateDecl = (FuncTerm)it.Current;
                    var qualifiedStateName = (FuncTerm)GetArgByIndex(stateDecl, 0);
                    var stateOwnerMachineName = GetMachineName(stateDecl, 1);
                    var stateName = GetNameFromQualifiedName(stateOwnerMachineName, qualifiedStateName);
                    var stateTable = allMachines[stateOwnerMachineName].stateNameToStateInfo[stateName];
                    it.MoveNext();
                    string eventName;
                    if (it.Current.NodeKind == NodeKind.Id)
                    {
                        var name = ((Id)it.Current).Name;
                        if (name == "NULL")
                        {
                            eventName = NullEvent;
                            stateTable.hasNullTransition = true;
                        }
                        else
                        {
                            // name == "HALT"
                            eventName = HaltEvent;
                        }
                    }
                    else
                    {
                        eventName = ((Cnst)it.Current).GetStringValue();
                    }
                    it.MoveNext();
                    var targetStateName = GetNameFromQualifiedName(stateOwnerMachineName, (FuncTerm)it.Current);
                    it.MoveNext();
                    if (it.Current.NodeKind == NodeKind.Id)
                    {
                        stateTable.transitions[eventName] = new TransitionInfo(targetStateName);
                    }
                    else
                    {
                        var exitFunName = it.Current.NodeKind == NodeKind.Cnst
                                            ? ((Cnst)it.Current).GetStringValue()
                                            : anonFunToName[Factory.Instance.ToAST(it.Current)];
                        stateTable.transitions[eventName] = new TransitionInfo(targetStateName, exitFunName);
                    }
                }
            }

            terms = GetBin(factBins, "DoDecl");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    var stateDecl = (FuncTerm)it.Current;
                    var qualifiedStateName = (FuncTerm)GetArgByIndex(stateDecl, 0);
                    var stateOwnerMachineName = GetMachineName(stateDecl, 1);
                    var stateName = GetNameFromQualifiedName(stateOwnerMachineName, qualifiedStateName);
                    var stateTable = allMachines[stateOwnerMachineName].stateNameToStateInfo[stateName];
                    it.MoveNext();
                    string eventName;
                    if (it.Current.NodeKind == NodeKind.Id)
                    {
                        var name = ((Id)it.Current).Name;
                        if (name == "NULL")
                        {
                            eventName = NullEvent;
                        }
                        else
                        {
                            // name == "HALT"
                            eventName = HaltEvent;
                        }
                    }
                    else
                    {
                        eventName = ((Cnst)it.Current).GetStringValue();
                    }
                    it.MoveNext();
                    var action = it.Current;
                    if (action.NodeKind == NodeKind.Cnst)
                    {
                        stateTable.actions[eventName] = ((Cnst)action).GetStringValue();
                    }
                    else if (action.NodeKind == NodeKind.Id)
                    {
                        if (((Id)action).Name == "DEFER")
                        {
                            stateTable.deferredEvents.Add(eventName);
                        }
                        else
                        {
                            // ((Id)action).Name == "IGNORE"
                            stateTable.ignoredEvents.Add(eventName);
                            stateTable.actions[eventName] = "ignore";
                        }
                    }
                    else
                    {
                        stateTable.actions[eventName] = anonFunToName[Factory.Instance.ToAST(action)];
                    }
                }
            }

            terms = GetBin(factBins, "Annotation");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    FuncTerm annotationContext = (FuncTerm)it.Current;
                    string annotationContextKind = ((Id)annotationContext.Function).Name;
                    if (annotationContextKind != "FunDecl") continue;
                    string ownerName = GetOwnerName(annotationContext, 1, 0);
                    string funName = GetName(annotationContext, 0);
                    it.MoveNext();
                    string annotation = ((Cnst)it.Current).GetStringValue();
                    if (annotation != "invokescheduler") continue;
                    it.MoveNext();
                    if (ownerName == null)
                    {
                        allStaticFuns[funName].invokeSchedulerFuns.Add(it.Current);
                    }
                    else
                    {
                        allMachines[ownerName].funNameToFunInfo[funName].invokeSchedulerFuns.Add(it.Current);
                    }
                }
            }

            if (compiler.Options.liveness != LivenessOption.None)
            {
                foreach (var machineName in allMachines.Keys)
                {
                    if (!allMachines[machineName].IsMonitor) continue;
                    var machineInfo = allMachines[machineName];
                    List<string> initialSet = new List<string>();
                    foreach (var stateName in ComputeReachableStates(machineInfo, new string[] { machineInfo.initStateName }))
                    {
                        if (machineInfo.stateNameToStateInfo[stateName].IsWarm)
                        {
                            continue;
                        }
                        if (machineInfo.stateNameToStateInfo[stateName].IsHot)
                        {
                            machineInfo.monitorType = MonitorType.FINALLY;
                            continue;
                        }
                        initialSet.Add(stateName);
                    }
                    foreach (var stateName in ComputeReachableStates(machineInfo, initialSet))
                    {
                        if (machineInfo.stateNameToStateInfo[stateName].IsHot)
                        {
                            machineInfo.monitorType = MonitorType.REPEATEDLY;
                            break;
                        }
                    }
                }
            }
        }

        HashSet<string> ComputeReachableStates(MachineInfo machineInfo, IEnumerable<string> initialSet)
        {
            Stack<string> dfsStack = new Stack<string>();
            HashSet<string> visitedStates = new HashSet<string>();
            foreach (var stateName in initialSet)
            {
                dfsStack.Push(stateName);
                visitedStates.Add(stateName);
            }
            while (dfsStack.Count > 0)
            {
                var curState = dfsStack.Pop();
                var curStateInfo = machineInfo.stateNameToStateInfo[curState];
                foreach (var e in curStateInfo.transitions.Keys)
                {
                    var nextState = curStateInfo.transitions[e].target;
                    if (visitedStates.Contains(nextState)) continue;
                    visitedStates.Add(nextState);
                    dfsStack.Push(nextState);
                }
            }
            return visitedStates;
        }

        void GenerateTypeInfo(AST<Model> model)
        {
            var factBins = new Dictionary<string, LinkedList<AST<FuncTerm>>>();
            model.FindAll(
                new NodePred[]
                {
                    NodePredFactory.Instance.Star,
                    NodePredFactory.Instance.MkPredicate(NodeKind.ModelFact)
                },

                (path, n) =>
                {
                    var mf = (ModelFact)n;
                    FuncTerm ft = (FuncTerm)mf.Match;
                    GetBin(factBins, ft).AddLast((AST<FuncTerm>)Factory.Instance.ToAST(ft));
                });
            var terms = GetBin(factBins, "TypeOf");
            foreach (var term in terms)
            {
                using (var it = term.Node.Args.GetEnumerator())
                {
                    it.MoveNext();
                    FuncTerm typingContext = (FuncTerm)it.Current;
                    it.MoveNext();
                    var expr = Factory.Instance.ToAST(it.Current);
                    it.MoveNext();
                    var type = it.Current as FuncTerm;
                    if (type == null) continue;

                    string typingContextKind = ((Id)typingContext.Function).Name;
                    if (typingContextKind == "FunDecl")
                    {
                        string ownerName = GetOwnerName(typingContext, 1, 0);
                        string funName = GetName(typingContext, 0);
                        if (ownerName == null)
                        {
                            allStaticFuns[funName].typeInfo[expr] = type;
                        }
                        else
                        {
                            allMachines[ownerName].funNameToFunInfo[funName].typeInfo[expr] = type;
                        }
                    }
                    else
                    {
                        // typingContextKind == "AnonFunDecl"
                        string ownerName = GetOwnerName(typingContext, 0, 0);
                        Debug.Assert(ownerName != null);
                        string funName = anonFunToName[Factory.Instance.ToAST(typingContext)];
                        allMachines[ownerName].funNameToFunInfo[funName].typeInfo[expr] = type;
                    }
                }
            }
        }

        #region Static helpers

        public static string SpanToString(Span span)
        {
            return string.Format("({0}, {1})", span.StartLine, span.StartCol);
        }

        public static string NodeToString(Node n)
        {
            System.IO.StringWriter sw = new System.IO.StringWriter();
            Factory.Instance.ToAST(n).Print(sw);
            return sw.ToString();
        }

        public static Node GetArgByIndex(FuncTerm ft, int index)
        {
            Contract.Requires(index >= 0 && index < ft.Args.Count);

            int i = 0;
            foreach (var a in ft.Args)
            {
                if (i == index)
                {
                    return a;
                }
                else
                {
                    ++i;
                }
            }

            throw new InvalidOperationException();
        }

        public static string GetName(FuncTerm ft, int nameIndex)
        {
            return ((Cnst)GetArgByIndex(ft, nameIndex)).GetStringValue();
        }

        public static string GetPrintedNameFromQualifiedName(FuncTerm qualifiedName)
        {
            var stateName = GetName(qualifiedName, 0);
            while (true)
            {
                qualifiedName = GetArgByIndex(qualifiedName, 1) as FuncTerm;
                if (qualifiedName == null) break;
                stateName = stateName + "." + GetName(qualifiedName, 0);
            }
            return stateName;
        }

        public static string GetNameFromQualifiedName(string machineName, FuncTerm qualifiedName)
        {
            var stateName = machineName;
            while (qualifiedName != null)
            {
                stateName = stateName + "_" + GetName(qualifiedName, 0);
                qualifiedName = GetArgByIndex(qualifiedName, 1) as FuncTerm;
            }
            return stateName;
        }

        public string GetOwnerName(FuncTerm ft, int ownerIndex, int ownerNameIndex)
        {
            var ownerArg = GetArgByIndex(ft, ownerIndex);
            switch (ownerArg.NodeKind)
            {
                case NodeKind.Id:
                    {
                        Debug.Assert(((Id)ownerArg).Name == "NIL");
                        return null;
                    }
                case NodeKind.FuncTerm:
                    return ((Cnst)GetArgByIndex((FuncTerm)ownerArg, ownerNameIndex)).GetStringValue();
                default:
                    throw new InvalidOperationException();
            }
        }

        public static AST<Model> Add(AST<Model> m, AST<FuncTerm> ft)
        {
            return Factory.Instance.AddFact(m, Factory.Instance.MkModelFact(null, ft));
        }

        public static AST<FuncTerm> AddArgs(AST<FuncTerm> ft, params AST<Node>[] args)
        {
            AST<FuncTerm> ret = ft;
            foreach (var v in args)
            {
                ret = Factory.Instance.AddArg(ret, v);
            }
            return ret;
        }

        public static AST<FuncTerm> AddArgs(AST<FuncTerm> ft, IEnumerable<AST<Node>> args)
        {
            AST<FuncTerm> ret = ft;
            foreach (var v in args)
            {
                ret = Factory.Instance.AddArg(ret, v);
            }
            return ret;
        }

        public static AST<Node> ConstructList(AST<FuncTerm> constructor, IEnumerable<AST<Node>> elems, AST<Node> def = null)
        {
            AST<Node> ret = def == null ? ZingData.Cnst_Nil : def;
            var reverseElems = new List<AST<Node>>(elems);
            reverseElems.Reverse();
            foreach (var n in reverseElems)
            {
                ret = Factory.Instance.AddArg(Factory.Instance.AddArg(constructor, n), ret);
            }

            return ret;
        }

        public static AST<Node> ConstructList(AST<FuncTerm> constructor, params AST<Node>[] elems)
        {
            return ConstructList(constructor, new List<AST<Node>>(elems));
        }
        private static AST<FuncTerm> MkZingFile(string filename, List<AST<Node>> elements)
        {
            return AddArgs(ZingData.App_File, Factory.Instance.MkCnst(filename), ConstructList(ZingData.App_Decls, elements));
        }

        private static AST<FuncTerm> MkZingIdentifier(string name)
        {
            return Factory.Instance.AddArg(ZingData.App_Identifier, Factory.Instance.MkCnst(name));
        }

        private static AST<FuncTerm> MkZingDot(string first, params string[] names)
        {
            AST<FuncTerm> lhs = MkZingIdentifier(first);
            for (int i = 0; i < names.Length; i++)
            {
                AST<FuncTerm> rhs = MkZingIdentifier(names[i]);
                lhs = MkZingApply(ZingData.Cnst_Dot, lhs, rhs);
            }
            return lhs;
        }

        private static AST<FuncTerm> MkZingDot(AST<Node> n, string first, params string[] names)
        {
            AST<FuncTerm> lhs = MkZingApply(ZingData.Cnst_Dot, n, MkZingIdentifier(first));
            for (int i = 0; i < names.Length; i++)
            {
                AST<FuncTerm> rhs = MkZingIdentifier(names[i]);
                lhs = MkZingApply(ZingData.Cnst_Dot, lhs, rhs);
            }
            return lhs;
        }

        private static AST<Node> MkZingEvent(string eventName)
        {
            return MkZingDot("Main", string.Format("{0}_SM_EVENT", eventName));
        }

        private static AST<Node> MkZingState(string stateName)
        {
            return MkZingDot("Main", string.Format("{0}_SM_STATE", stateName));
        }

        private static AST<FuncTerm> MkZingReturn(AST<Node> rVal)
        {
            return AddArgs(ZingData.App_Return, rVal);
        }

        public static AST<FuncTerm> MkZingIfThen(AST<Node> cond, AST<Node> then)
        {
            return AddArgs(ZingData.App_ITE, cond, then, ZingData.Cnst_Nil);
        }

        public static AST<FuncTerm> MkZingIfThenElse(AST<Node> cond, AST<Node> thenstmt, AST<Node> elsestmt)
        {
            return AddArgs(ZingData.App_ITE, cond, thenstmt, elsestmt);
        }

        private static AST<Node> MkZingNeq(AST<Node> e1, AST<Node> e2)
        {
            return MkZingApply(ZingData.Cnst_NEq, e1, e2);
        }

        private static AST<Node> MkZingEq(AST<Node> e1, AST<Node> e2)
        {
            return MkZingApply(ZingData.Cnst_Eq, e1, e2);
        }

        private static AST<Node> MkZingOr(AST<Node> e1, AST<Node> e2)
        {
            return MkZingApply(ZingData.Cnst_Or, e1, e2);
        }

        private static AST<Node> MkZingIn(AST<Node> e1, AST<Node> e2)
        {
            return MkZingApply(ZingData.Cnst_In, e1, e2);
        }

        private static void AddEventSet(List<AST<FuncTerm>> stmts, IEnumerable<string> eventNames, AST<FuncTerm> set)
        {
            foreach (var eventName in eventNames)
            {
                var addExpr = MkZingApply(ZingData.Cnst_Add, set, MkZingEvent(eventName));
                var addStmt = AddArgs(ZingData.App_Assign, set, addExpr);
                stmts.Add(addStmt);
            }
        }

        private static void SubtractEventSet(List<AST<FuncTerm>> stmts, IEnumerable<string> eventNames, AST<FuncTerm> actionSet)
        {
            foreach (var eventName in eventNames)
            {
                var subExpr = MkZingApply(ZingData.Cnst_Sub, actionSet, MkZingEvent(eventName));
                var subStmt = AddArgs(ZingData.App_Assign, actionSet, subExpr);
                stmts.Add(subStmt);
            }
        }
        private static AST<FuncTerm> MkZingCall(AST<Node> methodExpr, params AST<Node>[] args)
        {
            List<AST<Node>> argList = new List<AST<Node>>();
            argList.Add(methodExpr);
            foreach (var arg in args)
                argList.Add(arg);
            return Factory.Instance.AddArg(ZingData.App_Call, ConstructList(ZingData.App_Args, argList));
        }

        private static AST<FuncTerm> MkZingStrings(IEnumerable<AST<Node>> args)
        {
            List<AST<Node>> argList = new List<AST<Node>>();
            foreach (var arg in args)
                argList.Add(arg);
            return AddArgs(ZingData.App_Apply, args);
        }
        private static AST<FuncTerm> MkZingCall(AST<Node> methodExpr, IEnumerable<AST<Node>> args)
        {
            List<AST<Node>> argList = new List<AST<Node>>();
            argList.Add(methodExpr);
            foreach (var arg in args)
                argList.Add(arg);
            return Factory.Instance.AddArg(ZingData.App_Call, ConstructList(ZingData.App_Args, argList));
        }

        private static AST<FuncTerm> MkZingCallStmt(AST<Node> callExpr, params AST<Node>[] attrs)
        {
            return AddArgs(ZingData.App_CallStmt, callExpr, ConstructList(ZingData.App_Attrs, attrs));
        }

        private static AST<FuncTerm> MkZingMethodDecl(string methodName, AST<Node> parameters, AST<Node> returnType, AST<Node> locals, AST<Node> body, params AST<Node>[] attrs)
        {
            return AddArgs(ZingData.App_MethodDecl, Factory.Instance.MkCnst(methodName), parameters, returnType, locals, body, ConstructList(ZingData.App_Attrs, attrs));
        }

        private static AST<FuncTerm> MkZingGoto(string labelName)
        {
            return Factory.Instance.AddArg(ZingData.App_Goto, Factory.Instance.MkCnst(labelName));
        }

        private static AST<FuncTerm> MkZingBlock(string label, AST<Node> body)
        {
            return AddArgs(ZingData.App_LabelStmt, Factory.Instance.MkCnst(label), body);
        }

        private static AST<Node> MkZingBlocks(params AST<Node>[] blocks)
        {
            return ConstructList(ZingData.App_Blocks, blocks);
        }

        private static AST<Node> MkZingBlocks(List<AST<Node>> blocks)
        {
            return ConstructList(ZingData.App_Blocks, blocks);
        }

        private static AST<Node> MkZingNew(AST<Node> constructedType, AST<Node> optionalSize)
        {
            return AddArgs(ZingData.App_New, constructedType, optionalSize);
        }

        private static AST<FuncTerm> MkZingAssign(AST<Node> lhs, AST<Node> rhs)
        {
            return AddArgs(ZingData.App_Assign, lhs, rhs);
        }

        private static AST<FuncTerm> MkZingVarDecl(string varName, AST<Node> varType, params AST<Node>[] attrs)
        {
            return AddArgs(ZingData.App_VarDecl, Factory.Instance.MkCnst(varName), varType, ConstructList(ZingData.App_Attrs, attrs));
        }

        private static AST<Node> MkZingVarDecls(IEnumerable<AST<Node>> vars)
        {
            return ConstructList(ZingData.App_VarDecls, vars);
        }

        private static AST<Node> MkZingEnumDecl(string enumName, List<AST<Node>> enumElems)
        {
            return AddArgs(ZingData.App_EnumDecl, Factory.Instance.MkCnst(enumName), ConstructList(ZingData.App_EnumElems, enumElems));
        }

        private static AST<Node> MkZingMethodDecls(IEnumerable<AST<Node>> decls)
        {
            return ConstructList(ZingData.App_MethodDecls, decls);
        }

        private static AST<FuncTerm> MkZingApply(AST<Id> op, params AST<Node>[] args)
        {
            return AddArgs(ZingData.App_Apply, op, ConstructList(ZingData.App_Args, args));
        }

        private AST<Node> MkZingActionOrFun(string machineName, string entityName)
        {
            if (allMachines[machineName].funNameToFunInfo.ContainsKey(entityName))
            {
                return MkZingDot("ActionOrFun", string.Format("_{0}_{1}", machineName, entityName));
            }
            else
            {
                return MkZingDot("ActionOrFun", string.Format("_{0}", entityName));
            }
        }

        private static AST<Node> MkZingVarDecls(params AST<Node>[] vars)
        {
            return ConstructList(ZingData.App_VarDecls, vars);
        }

        private static AST<Node> MkZingIndex(AST<Node> baseExp, AST<Node> indExp)
        {
            return MkZingApply(ZingData.Cnst_Index, baseExp, indExp);
        }

        private static AST<Node> MkZingSeq(List<AST<Node>> stmts)
        {
            AST<Node> ret = ZingData.Cnst_Nil;
            foreach (var stmt in stmts.Reverse<AST<Node>>())
            {
                ret = AddArgs(ZingData.App_Seq, stmt, ret);
            }
            return ret;
        }

        private static AST<Node> MkZingSeq(params AST<Node>[] stmts)
        {
            return MkZingSeq(new List<AST<Node>>(stmts));
        }

        private static AST<Node> MkZingWhile(AST<Node> condition, AST<Node> stmt)
        {
            return AddArgs(ZingData.App_While, condition, stmt);
        }

        private static AST<FuncTerm> MkZingAssert(AST<Node> condition)
        {
            return AddArgs(ZingData.App_Assert, condition, ZingData.Cnst_Nil);
        }

        private static AST<FuncTerm> MkZingAssert(AST<Node> condition, string msg)
        {
            return AddArgs(ZingData.App_Assert, condition, Factory.Instance.MkCnst(msg));
        }

        private static AST<FuncTerm> MkZingAssume(AST<Node> condition)
        {
            return AddArgs(ZingData.App_Assume, condition);
        }

        private static AST<FuncTerm> MkZingAdd(AST<Node> a, AST<Node> b)
        {
            return MkZingApply(ZingData.Cnst_Add, a, b);
        }

        private static AST<FuncTerm> MkZingSub(AST<Node> a, AST<Node> b)
        {
            return MkZingApply(ZingData.Cnst_Sub, a, b);
        }

        private static AST<FuncTerm> MkZingNot(AST<Node> a)
        {
            return MkZingApply(ZingData.Cnst_Not, a);
        }
        #endregion

        #region ZingCompiler
        public void GenerateZing(string zingFileName, ref AST<Model> outModel)
        {
            List<AST<Node>> elements = new List<AST<Node>>();
            MkZingEnums(elements);
            MkZingClasses(elements);
            outModel = Add(outModel, MkZingFile(zingFileName, elements));
        }

        private void MkZingEnums(List<AST<Node>> elements)
        {
            List<AST<Node>> machineConsts = new List<AST<Node>>();
            machineConsts.Add(Factory.Instance.MkCnst("_default"));
            foreach (string machineName in allMachines.Keys)
            {
                machineConsts.Add(Factory.Instance.MkCnst(string.Format("_{0}", machineName)));
            }
            elements.Add(MkZingEnumDecl("Machine", machineConsts));

            List<AST<Node>> eventConsts = new List<AST<Node>>();
            eventConsts.Add(Factory.Instance.MkCnst("_default"));
            foreach (string eventName in allEvents.Keys.Where(x => x != NullEvent))
            {
                eventConsts.Add(Factory.Instance.MkCnst(string.Format("_{0}", eventName)));
            }
            elements.Add(MkZingEnumDecl("Event", eventConsts));

            List<AST<Node>> stateConsts = new List<AST<Node>>();
            stateConsts.Add(Factory.Instance.MkCnst("_default"));
            foreach (var machine in allMachines.Values)
            {
                foreach (var stateName in machine.stateNameToStateInfo.Keys)
                {
                    stateConsts.Add(Factory.Instance.MkCnst(string.Format("_{0}", stateName)));
                }
            }
            elements.Add(MkZingEnumDecl("State", stateConsts));

            List<AST<Node>> actionOrFunConsts = new List<AST<Node>>();
            actionOrFunConsts.Add(Factory.Instance.MkCnst("_default"));
            foreach (string funName in allStaticFuns.Keys)
            {
                actionOrFunConsts.Add(Factory.Instance.MkCnst(string.Format("_{0}", funName)));
            }
            foreach (string machineName in allMachines.Keys)
            {
                foreach (string funName in allMachines[machineName].funNameToFunInfo.Keys)
                {
                    actionOrFunConsts.Add(Factory.Instance.MkCnst(string.Format("_{0}_{1}", machineName, funName)));
                }
            }
            elements.Add(MkZingEnumDecl("ActionOrFun", actionOrFunConsts));

            foreach (var machineName in allMachines.Keys)
            {
                if (!allMachines[machineName].IsMonitor) continue;
                elements.Add(AddArgs(ZingData.App_SetDecl, Factory.Instance.MkCnst(ZingMachineClassSetTypeName(machineName)), 
                                                           Factory.Instance.MkCnst(ZingMachineClassName(machineName))));
            }
        }

        private string GetMonitorMachineName(string machineName)
        {
            return string.Format("{0}_handles", machineName);
        }

        private string GetFairChoice(string entityName, int i)
        {
            return string.Format("FairChoice_{0}_{1}", entityName, i);
        }

        private AST<FuncTerm> GenerateMainClass()
        {
            // Generate Main
            List<AST<Node>> fields = new List<AST<Node>>();
            foreach (var eventName in allEvents.Keys)
            {
                fields.Add(MkZingVarDecl(string.Format("{0}_SM_EVENT", eventName), SmEvent, ZingData.Cnst_Static));
            }
            foreach (var machine in allMachines.Values)
            {
                foreach (var stateName in machine.stateNameToStateInfo.Keys)
                {
                    fields.Add(MkZingVarDecl(string.Format("{0}_SM_STATE", stateName), SmState, ZingData.Cnst_Static));
                }
            }
            foreach (var machineName in allMachines.Keys)
            {
                fields.Add(MkZingVarDecl(string.Format("{0}_instance", machineName), ZingData.Cnst_Int, ZingData.Cnst_Static));
            }
            foreach (var machineName in allMachines.Keys)
            {
                if (!allMachines[machineName].IsMonitor) continue;
                fields.Add(MkZingVarDecl(GetMonitorMachineName(machineName), Factory.Instance.MkCnst(ZingMachineClassSetTypeName(machineName)), ZingData.Cnst_Static));
            }

            List<AST<Node>> methods = new List<AST<Node>>();
            foreach (var machineName in allMachines.Keys)
            {
                AST<Node> method;
                if (allMachines[machineName].IsMonitor)
                {
                    method = MkCreateMonitorMethod(machineName);
                }
                else
                {
                    method = MkCreateMachineMethod(machineName);
                }
                methods.Add(method);
            }
            foreach (var machineName in allMachines.Keys)
            {
                if (!allMachines[machineName].IsMonitor) continue;
                AST<Node> method = MkInvokeMonitorMethod(machineName);
                methods.Add(method);
            }

            // Generate method for computing complement of a set of events
            AST<Node> calculateComplementParameters = MkZingVarDecls(MkZingVarDecl("eventSet", SmEventSet));
            AST<Node> calculateComplementLocalVars = MkZingVarDecls(MkZingVarDecl("returnEventSet", SmEventSet));
            AST<Node> calculateComplementBody = MkZingAssign(MkZingIdentifier("returnEventSet"), MkZingNew(SmEventSet, ZingData.Cnst_Nil));
            foreach (var eventName in allEvents.Keys)
            {
                if (eventName == HaltEvent)
                    continue;
                var iteExpr = MkZingIn(MkZingEvent(eventName), MkZingIdentifier("eventSet"));
                var assignStmt = MkZingAssign(MkZingIdentifier("returnEventSet"), MkZingApply(ZingData.Cnst_Add, MkZingIdentifier("returnEventSet"), MkZingEvent(eventName)));
                var iteStmt = MkZingIfThenElse(iteExpr, ZingData.Cnst_Nil, assignStmt);
                calculateComplementBody = MkZingSeq(calculateComplementBody, iteStmt);
            }
            calculateComplementBody = MkZingSeq(calculateComplementBody, MkZingReturn(MkZingIdentifier("returnEventSet")));
            calculateComplementBody = MkZingBlock("dummy", calculateComplementBody);
            calculateComplementBody = MkZingBlocks(calculateComplementBody);
            AST<Node> calculateComplementMethod = MkZingMethodDecl("CalculateComplementOfEventSet", calculateComplementParameters, SmEventSet, calculateComplementLocalVars, calculateComplementBody, ZingData.Cnst_Static);
            methods.Add(calculateComplementMethod);

            // Generate the PayloadOf static method. PayloadOf :: (EventId) -> Discriminator
            // Returns the statically declared type for the given event.
            var payloadOfBody = new List<AST<Node>>();
            payloadOfBody.Add(MkZingIfThen(MkZingEq(MkZingIdentifier("e"), MkZingIdentifier("null")),
                MkZingReturn(typeContext.PTypeToZingExpr(PTypeNull.Node))));
            foreach (var evt in allEvents.Keys.Where(x => x != NullEvent))
            {
                payloadOfBody.Add(MkZingIfThenElse(MkZingEq(MkZingDot("e", "name"), MkZingDot("Event", "_" + evt)),
                    MkZingReturn(typeContext.PTypeToZingExpr(allEvents[evt].payloadType)),
                    ZingData.Cnst_Nil));
            }
            payloadOfBody.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));
            AST<Node> payloadOfMethod = MkZingMethodDecl("PayloadOf",
                MkZingVarDecls(MkZingVarDecl("e", Factory.Instance.MkCnst("SM_EVENT"))),
                Factory.Instance.MkCnst("PRT_TYPE"), ZingData.Cnst_Nil, MkZingBlocks(MkZingBlock("dummy", MkZingSeq(payloadOfBody))), ZingData.Cnst_Static);
            methods.Add(payloadOfMethod);

            foreach (var funName in allStaticFuns.Keys)
            {
                var funInfo = allStaticFuns[funName];
                methods.Add(MkZingStaticFunMethod(funName, funInfo));
            }

            fields.AddRange(typeContext.MainVarDecls());

            List<AST<Node>> runBodyStmts = new List<AST<Node>>();
            foreach (var eventName in allEvents.Keys)
            {
                AST<Node> rhs;
                if (eventName == NullEvent)
                    rhs = MkZingIdentifier("null");
                else
                    rhs = MkZingCall(
                                    MkZingDot("SM_EVENT", "Construct"),
                                    MkZingDot("Event", string.Format("_{0}", eventName)),
                                    Factory.Instance.MkCnst(allEvents[eventName].maxInstances),
                                    allEvents[eventName].maxInstancesAssumed ? ZingData.Cnst_True : ZingData.Cnst_False);

                var assignStmt = MkZingAssign(MkZingEvent(eventName), rhs);
                runBodyStmts.Add(assignStmt);
            }
            foreach (var machineName in allMachines.Keys)
            {
                var machine = allMachines[machineName];
                foreach (var stateName in machine.stateNameToStateInfo.Keys)
                {
                    var stateInfo = machine.stateNameToStateInfo[stateName];
                    var stateTemperature = stateInfo.temperature == StateTemperature.COLD
                                           ? MkZingDot("StateTemperature", "Cold")
                                           : (stateInfo.temperature == StateTemperature.HOT ? MkZingDot("StateTemperature", "Hot") : MkZingDot("StateTemperature", "Warm"));
                    var state = MkZingCall(
                                        MkZingDot("SM_STATE", "Construct"), 
                                        MkZingDot("State", string.Format("_{0}", stateName)),
                                        MkZingActionOrFun(machineName, stateInfo.entryActionName),
                                        MkZingActionOrFun(machineName, stateInfo.exitFunName),
                                        Factory.Instance.MkCnst(stateInfo.transitions.Count),
                                        stateInfo.hasNullTransition ? ZingData.Cnst_True : ZingData.Cnst_False,
                                        stateTemperature
                                        );
                    runBodyStmts.Add(MkZingAssign(MkZingState(stateName), state));
                }
            }
            foreach (var machineName in allMachines.Keys)
            {
                var machine = allMachines[machineName];
                foreach (var stateName in machine.stateNameToStateInfo.Keys)
                {
                    var stateInfo = machine.stateNameToStateInfo[stateName];
                    int i = 0;
                    foreach (var eventName in stateInfo.transitions.Keys)
                    {
                        var transitionInfo = stateInfo.transitions[eventName];
                        var transition = MkZingCall(
                                            MkZingDot("SM_TRANSITION", "Construct"),
                                            MkZingEvent(eventName),
                                            transitionInfo.IsPush ? MkZingDot("ActionOrFun", "_default") : MkZingActionOrFun(machineName, transitionInfo.transFunName),
                                            MkZingState(transitionInfo.target)
                                            );
                        var lhs = MkZingIndex(MkZingDot(MkZingState(stateName), "transitions"), Factory.Instance.MkCnst(i));
                        runBodyStmts.Add(MkZingAssign(lhs, transition));
                        i++;
                    }
                }
            }
            foreach (var machineName in allMachines.Keys)
            {
                var assignStmt = MkZingAssign(MkZingIdentifier(string.Format("{0}_instance", machineName)), Factory.Instance.MkCnst(0));
                runBodyStmts.Add(assignStmt);
            }
            foreach (var machineName in allMachines.Keys)
            {
                if (allMachines[machineName].IsMonitor)
                {
                    var assignStmt = MkZingAssign(MkZingIdentifier(GetMonitorMachineName(machineName)), 
                                                  MkZingNew(Factory.Instance.MkCnst(ZingMachineClassSetTypeName(machineName)), ZingData.Cnst_Nil));
                    runBodyStmts.Add(assignStmt);
                }
            }
            runBodyStmts.Add(typeContext.InitializeFieldNamesAndTypes());
            runBodyStmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("Main", string.Format("CreateMachine_{0}", mainMachineName)), MkZingIdentifier("null"))));
            AST<Node> runMethod = MkZingMethodDecl("Run", ZingData.Cnst_Nil, ZingData.Cnst_Void, ZingData.Cnst_Nil, MkZingBlocks(MkZingBlock("dummy", MkZingSeq(runBodyStmts))), ZingData.Cnst_Static, ZingData.Cnst_Activate);
            methods.Add(runMethod);

            return AddArgs(ZingData.App_ClassDecl, Factory.Instance.MkCnst("Main"), MkZingVarDecls(fields), MkZingMethodDecls(methods));
        }

        private AST<FuncTerm> GenerateMachineClass(string machineName)
        {
            List<AST<Node>> fields = new List<AST<Node>>(allMachines[machineName].localVariableToVarInfo.Keys.Select(name => MkZingVarDecl(name, PrtValue)));
            fields.Add(MkZingVarDecl("myHandle", SmHandle));

            List<AST<Node>> methods = new List<AST<Node>>();
            foreach (var x in allMachines[machineName].stateNameToStateInfo)
            {
                AST<Node> dequeueEventMethod = GenerateCalculateDeferredAndActionSetMethodDecl(x.Key, x.Value);
                methods.Add(dequeueEventMethod);
            }
            AST<Node> startMethod = GenerateStartMethodDecl(machineName);
            methods.Add(startMethod);
            AST<Node> runMethod = GenerateRunMethodDecl();
            methods.Add(runMethod);
            AST<Node> traceEnterStateMethod = GenerateTraceEnterStateMethodDecl(machineName);
            methods.Add(traceEnterStateMethod);
            AST<Node> traceExitStateMethod = GenerateTraceExitStateMethodDecl(machineName);
            methods.Add(traceExitStateMethod);
            AST<Node> calculateDeferredAndActionSetMethod = GenerateCalculateDeferredAndActionSetMethodDecl(machineName);
            methods.Add(calculateDeferredAndActionSetMethod);
            AST<Node> runHelperMethod = GenerateRunHelperMethodDecl(machineName);
            methods.Add(runHelperMethod);
            AST<Node> pushMethod = GeneratePushMethodDecl(machineName);
            methods.Add(pushMethod);
            AST<Node> processContinuationMethod = GenerateProcessContinuationMethodDecl();
            methods.Add(processContinuationMethod);
            AST<Node> actionHelperMethod = GenerateReentrancyHelperMethodDecl(machineName);
            methods.Add(actionHelperMethod);
            foreach (var funName in allMachines[machineName].funNameToFunInfo.Keys)
            {
                var funInfo = allMachines[machineName].funNameToFunInfo[funName];
                methods.Add(MkZingFunMethod(machineName, funName, funInfo));
            }
            foreach (var funName in allMachines[machineName].funNameToFunInfo.Keys)
            {
                var funInfo = allMachines[machineName].funNameToFunInfo[funName];
                for (int i = 0; i < funInfo.numFairChoices; i++)
                {
                    fields.Add(MkZingVarDecl(GetFairChoice(funName, i), Factory.Instance.MkCnst("FairChoice")));
                }
            }
            return AddArgs(ZingData.App_ClassDecl, Factory.Instance.MkCnst(ZingMachineClassName(machineName)), MkZingVarDecls(fields), MkZingMethodDecls(methods));
        }

        private void MkZingClasses(List<AST<Node>> elements)
        {
            foreach (string machineName in allMachines.Keys)
            {
                elements.Add(GenerateMachineClass(machineName));
            }
            elements.Add(GenerateMainClass());
        }

        private AST<Node> GenerateCalculateDeferredAndActionSetMethodDecl(string stateName, StateInfo stateInfo)
        {
            var smEventSetType = Factory.Instance.MkCnst("SM_EVENT_SET");
            var currentDeferredSet = MkZingDot("myHandle", "stack", "deferredSet");
            var currentActionSet = MkZingDot("myHandle", "stack", "actionSet");

            var ownerName = stateInfo.ownerName;
            var localDeferredSet = stateInfo.deferredEvents;
            var actions = stateInfo.actions;
            var transitions = stateInfo.transitions;

            List<AST<FuncTerm>> stmts = new List<AST<FuncTerm>>();
            stmts.Add(MkZingAssign(currentDeferredSet, MkZingNew(smEventSetType, ZingData.Cnst_Nil)));
            stmts.Add(MkZingAssign(currentActionSet, MkZingNew(smEventSetType, ZingData.Cnst_Nil)));
            stmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("myHandle", "stack", "AddStackDeferredSet"), currentDeferredSet)));
            AddEventSet(stmts, localDeferredSet, currentDeferredSet);
            SubtractEventSet(stmts, actions.Keys, currentDeferredSet);
            SubtractEventSet(stmts, transitions.Keys, currentDeferredSet);
            stmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("myHandle", "stack", "AddStackActionSet"), currentActionSet)));
            SubtractEventSet(stmts, localDeferredSet, currentActionSet);
            AddEventSet(stmts, actions.Keys, currentActionSet);
            SubtractEventSet(stmts, transitions.Keys, currentActionSet);
            stmts.Add(MkZingAssign(MkZingDot("myHandle", "stack", "es"), MkZingNew(Factory.Instance.MkCnst("SM_EVENT_ARRAY"), Factory.Instance.MkCnst(actions.Count))));
            stmts.Add(MkZingAssign(MkZingDot("myHandle", "stack", "as"), MkZingNew(Factory.Instance.MkCnst("ActionOrFun_ARRAY"), Factory.Instance.MkCnst(actions.Count))));
            int count = 0;
            foreach (var eventName in actions.Keys)
            {
                var actionName = actions[eventName];
                stmts.Add(MkZingAssign(MkZingApply(ZingData.Cnst_Index, MkZingDot("myHandle", "stack", "es"), Factory.Instance.MkCnst(count)), MkZingEvent(eventName)));
                stmts.Add(MkZingAssign(MkZingApply(ZingData.Cnst_Index, MkZingDot("myHandle", "stack", "as"), Factory.Instance.MkCnst(count)), MkZingActionOrFun(ownerName, actionName)));
                count = count + 1;
            }

            var body = MkZingBlock("dummy", MkZingSeq(stmts.ToArray()));
            return MkZingMethodDecl(string.Format("{0}_CalculateDeferredAndActionSet", stateName), ZingData.Cnst_Nil, ZingData.Cnst_Void, ZingData.Cnst_Nil, MkZingBlocks(body));
        }

        private AST<Node> GenerateStartMethodDecl(string machineName)
        {
            string initStateName = allMachines[machineName].initStateName;
            var callStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier("Run"), MkZingState(initStateName)));
            var iteStmt = MkZingIfThen(MkZingOr(MkZingEq(MkZingDot("myHandle", "currentEvent"), MkZingEvent("halt")), 
                                                MkZingEq(MkZingDot("myHandle", "currentEvent"), MkZingEvent("null"))),
                                    MkZingSeq(
                                        MkZingAssign(MkZingDot("myHandle", "stack"), MkZingIdentifier("null")),
                                        MkZingAssign(MkZingDot("myHandle", "buffer"), MkZingIdentifier("null")),
                                        MkZingAssign(MkZingDot("myHandle", "currentArg"), MkZingIdentifier("null")),
                                        MkZingAssign(MkZingDot("SM_HANDLE", "halted"), MkZingAdd(MkZingDot("SM_HANDLE", "halted"), MkZingIdentifier("myHandle"))),
                                        MkZingAssign(MkZingDot("SM_HANDLE", "enabled"), MkZingSub(MkZingDot("SM_HANDLE", "enabled"), MkZingIdentifier("myHandle"))),
                                        MkZingReturn(ZingData.Cnst_Nil))
                                        );
            string traceString = string.Format("\"<StateLog> Unhandled event exception by machine {0}-{{0}}\\n\"", machineName);
            var traceStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance")));
            var assertStmt = MkZingAssert(ZingData.Cnst_False, string.Format("Unhandled event exception by machine {0}", machineName));
            var body = MkZingBlocks(MkZingBlock("dummy", MkZingSeq(callStmt, iteStmt, traceStmt, assertStmt)));
            return MkZingMethodDecl("Start", ZingData.Cnst_Nil, ZingData.Cnst_Void, ZingData.Cnst_Nil, body);
        }

        private AST<Node> GenerateRunMethodDecl()
        {
            List<AST<Node>> parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("state", SmState));

            List<AST<Node>> locals = new List<AST<Node>>();
            locals.Add(MkZingVarDecl("doPop", ZingData.Cnst_Bool));
            locals.Add(MkZingVarDecl("hasNullTransitionOrAction", ZingData.Cnst_Bool));

            var doPop = MkZingIdentifier("doPop");
            var hasNullTransitionOrAction = MkZingIdentifier("hasNullTransitionOrAction");

            List<AST<Node>> initStmts = new List<AST<Node>>();
            initStmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("myHandle", "Push"))));
            initStmts.Add(MkZingAssign(MkZingDot("myHandle", "stack", "state"), MkZingIdentifier("state")));
            initStmts.Add(MkZingAssign(doPop, MkZingCall(MkZingIdentifier("RunHelper"), ZingData.Cnst_True)));
            var whileStmts = new List<AST<Node>>();
            whileStmts.Add(MkZingAssign(hasNullTransitionOrAction, MkZingCall(MkZingDot("myHandle", "stack", "HasNullTransitionOrAction"))));
            whileStmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("myHandle", "DequeueEvent"), hasNullTransitionOrAction)));
            whileStmts.Add(MkZingAssign(doPop, MkZingCall(MkZingIdentifier("RunHelper"), ZingData.Cnst_False)));
            initStmts.Add(MkZingWhile(MkZingNot(doPop), MkZingSeq(whileStmts)));
            initStmts.Add(MkZingCallStmt(MkZingCall(MkZingDot("myHandle", "Pop"))));

            var initBlock = MkZingBlock("init", MkZingSeq(initStmts));
            return MkZingMethodDecl("Run", MkZingVarDecls(parameters), ZingData.Cnst_Void, MkZingVarDecls(locals), MkZingBlocks(initBlock));
        }

        private AST<Node> GeneratePushMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("cont", Factory.Instance.MkCnst("Continuation")));
            List<AST<Node>> locals = new List<AST<Node>>();
            locals.Add(MkZingVarDecl("savedDeferredSet", SmEventSet));
            locals.Add(MkZingVarDecl("savedCurrentEvent", SmEvent));
            locals.Add(MkZingVarDecl("savedCurrentArg", PrtValue));

            var currentEvent = MkZingDot("myHandle", "currentEvent");
            var currentArg = MkZingDot("myHandle", "currentArg");
            var savedCurrentEvent = MkZingIdentifier("savedCurrentEvent");
            var savedCurrentArg = MkZingIdentifier("savedCurrentArg");
            var savedDeferredSet = MkZingIdentifier("savedDeferredSet");
            var cont = MkZingIdentifier("cont");

            List<AST<Node>> body = new List<AST<Node>>();
            body.Add(MkZingAssign(savedCurrentEvent, currentEvent));
            body.Add(MkZingAssign(savedCurrentArg, currentArg));
            body.Add(MkZingAssign(savedDeferredSet, MkZingDot("myHandle", "stack", "deferredSet")));
            body.Add(MkZingAssign(currentEvent, MkZingIdentifier("null")));
            body.Add(MkZingAssign(MkZingDot("myHandle", "currentArg"), MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
            body.Add(MkZingAssign(MkZingDot("myHandle", "stack", "deferredSet"), MkZingCall(MkZingDot("Main", "CalculateComplementOfEventSet"), MkZingDot("myHandle", "stack", "actionSet"))));
            body.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("Run"), MkZingDot("cont", "state"))));
            body.Add(MkZingAssign(MkZingDot("cont", "state"), MkZingIdentifier("null")));
            body.Add(MkZingIfThenElse(
                      MkZingApply(ZingData.Cnst_Eq, currentEvent, MkZingIdentifier("null")),
                      MkZingSeq(MkZingAssign(currentEvent, savedCurrentEvent), MkZingAssign(currentArg, savedCurrentArg), MkZingAssign(MkZingDot("myHandle", "stack", "deferredSet"), savedDeferredSet), MkZingReturn(ZingData.Cnst_False)),
                      MkZingSeq(MkZingAssign(MkZingDot("myHandle", "stack", "deferredSet"), savedDeferredSet), MkZingCallStmt(MkZingCall(MkZingDot(cont, "Raise"))), MkZingReturn(ZingData.Cnst_True))));

            return MkZingMethodDecl("Push", parameters, ZingData.Cnst_Bool, MkZingVarDecls(locals), MkZingBlocks(MkZingBlock("init", MkZingSeq(body))));
        }

        private AST<Node> GenerateReentrancyHelperMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("actionFun", Factory.Instance.MkCnst("ActionOrFun")));
            List<AST<Node>> locals = new List<AST<Node>>();
            locals.Add(MkZingVarDecl("cont", Factory.Instance.MkCnst("Continuation")));
            locals.Add(MkZingVarDecl("doPop", ZingData.Cnst_Bool));

            var cont = MkZingIdentifier("cont");
            var doPop = MkZingIdentifier("doPop");

            List<AST<Node>> initStmts = new List<AST<Node>>();
            initStmts.Add(MkZingAssign(cont, MkZingCall(MkZingDot(Factory.Instance.MkCnst("Continuation"), "Construct_Default"))));
            initStmts.Add(MkZingCallStmt(MkZingCall(MkZingDot(cont, "PushReturnTo"), Factory.Instance.MkCnst(0), MkZingIdentifier("null"))));
            initStmts.Add(MkZingAssign(doPop, ZingData.Cnst_False));
            foreach (var funName in allStaticFuns.Keys)
            {
                var funInfo = allStaticFuns[funName];
                if (funInfo.parameterNames.Count > 0) continue;
                var funExpr = MkZingActionOrFun(machineName, funName);
                var condExpr = MkZingApply(ZingData.Cnst_Eq, MkZingIdentifier("actionFun"), funExpr);
                var gotoStmt = MkZingGoto("execute_" + funName);
                string traceString = string.Format("\"<FunctionLog> Machine {0}-{{0}} executing Function {1}\\n\"", machineName, funName);
                var traceStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance")));
                initStmts.Add(MkZingIfThen(condExpr, MkZingSeq(traceStmt, gotoStmt)));
            }
            foreach (var funName in allMachines[machineName].funNameToFunInfo.Keys)
            {
                var funInfo = allMachines[machineName].funNameToFunInfo[funName];
                if (funInfo.parameterNames.Count > 0) continue;
                var funExpr = MkZingActionOrFun(machineName, funName);
                var condExpr = MkZingApply(ZingData.Cnst_Eq, MkZingIdentifier("actionFun"), funExpr);
                var gotoStmt = MkZingGoto("execute_" + funName);
                if (funInfo.isAnonymous)
                {
                    initStmts.Add(MkZingIfThen(condExpr, gotoStmt));
                }
                else
                {
                    string traceString = string.Format("\"<FunctionLog> Machine {0}-{{0}} executing Function {1}\\n\"", machineName, funName);
                    var traceStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance")));
                    initStmts.Add(MkZingIfThen(condExpr, MkZingSeq(traceStmt, gotoStmt)));
                }
            }
            initStmts.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));
            AST<Node> initStmt = MkZingBlock("init", MkZingSeq(initStmts));

            // Action blocks
            List<AST<Node>> blocks = new List<AST<Node>>();
            blocks.Add(initStmt);
            foreach (var funName in allStaticFuns.Keys)
            {
                if (allStaticFuns[funName].parameterNames.Count > 0) continue;
                var callStmt = MkZingCallStmt(MkZingCall(MkZingDot("Main", funName), MkZingIdentifier("myHandle"), cont));
                List<AST<Node>> whileStmts = new List<AST<Node>>();
                whileStmts.Add(callStmt);
                whileStmts.Add(MkZingAssign(doPop, MkZingCall(MkZingIdentifier("ProcessContinuation"), cont)));
                blocks.Add(MkZingBlock("execute_" + funName, MkZingSeq(MkZingWhile(MkZingNot(doPop), MkZingSeq(whileStmts)), MkZingReturn(cont))));
            }
            foreach (var funName in allMachines[machineName].funNameToFunInfo.Keys)
            {
                if (allMachines[machineName].funNameToFunInfo[funName].parameterNames.Count > 0) continue;
                var callStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier(funName), cont));
                List<AST<Node>> whileStmts = new List<AST<Node>>();
                whileStmts.Add(callStmt);
                whileStmts.Add(MkZingAssign(doPop, MkZingCall(MkZingIdentifier("ProcessContinuation"), cont)));
                blocks.Add(MkZingBlock("execute_" + funName, MkZingSeq(MkZingWhile(MkZingNot(doPop), MkZingSeq(whileStmts)), MkZingReturn(cont))));                
            }
            return MkZingMethodDecl("ReentrancyHelper", parameters, Factory.Instance.MkCnst("Continuation"), MkZingVarDecls(locals), MkZingBlocks(blocks));
        }
        
        private AST<Node> GenerateProcessContinuationMethodDecl()
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("cont", Factory.Instance.MkCnst("Continuation")));
            List<AST<Node>> locals = new List<AST<Node>>();
            locals.Add(MkZingVarDecl("doPop", ZingData.Cnst_Bool));

            var cont = MkZingIdentifier("cont");
            var doPop = MkZingIdentifier("doPop");

            var body = new List<AST<Node>>();
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Return")), MkZingReturn(ZingData.Cnst_True)));
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Pop")), MkZingReturn(ZingData.Cnst_True)));
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Raise")), MkZingReturn(ZingData.Cnst_True)));
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Push")),
                                  MkZingSeq(MkZingAssign(doPop, MkZingCall(MkZingIdentifier("Push"), cont)), MkZingReturn(doPop))));
            AST<Node> atChooseLivenessStmt = ZingData.Cnst_Nil;
            AST<Node> atYieldLivenessStmt = ZingData.Cnst_Nil;
            if (compiler.Options.liveness == LivenessOption.Standard)
            {
                atChooseLivenessStmt = MkZingSeq(
                    MkZingCallStmt(MkZingCall(MkZingDot("FairCycle", "GateProgress"))),
                    MkZingCallStmt(MkZingCall(MkZingDot("FairScheduler", "AtChooseStatic"))),
                    MkZingCallStmt(MkZingCall(MkZingDot("FairChoice", "AtYieldOrChooseStatic"))));
                atYieldLivenessStmt = MkZingSeq(
                    MkZingCallStmt(MkZingCall(MkZingDot("FairCycle", "GateProgress"))),
                    MkZingCallStmt(MkZingCall(MkZingDot("FairScheduler", "AtYieldStatic"), MkZingIdentifier("myHandle"))),
                    MkZingCallStmt(MkZingCall(MkZingDot("FairChoice", "AtYieldOrChooseStatic"))));
            }
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Nondet")),
                MkZingSeq(
                    atChooseLivenessStmt,
                    MkZingAssign(MkZingDot(cont, "nondet"), MkZingCall(Factory.Instance.MkCnst("choose"), Factory.Instance.MkCnst("bool"))),
                    MkZingReturn(ZingData.Cnst_False))));
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "NewMachine")),
                MkZingSeq(
                    atYieldLivenessStmt,
                    ZingData.Cnst_Yield,
                    MkZingReturn(ZingData.Cnst_False))));
            body.Add(MkZingIfThen(MkZingEq(MkZingDot("cont", "reason"), MkZingDot("ContinuationReason", "Send")),
                MkZingSeq(
                    atYieldLivenessStmt,
                    ZingData.Cnst_Yield,
                    MkZingReturn(ZingData.Cnst_False))));

            return MkZingMethodDecl("ProcessContinuation", parameters, ZingData.Cnst_Bool, MkZingVarDecls(locals), MkZingBlocks(MkZingBlock("init", MkZingSeq(body))));
        }

        private AST<Node> GenerateTraceEnterStateMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("state", SmState));
            List<AST<Node>> locals = new List<AST<Node>>();

            var state = MkZingIdentifier("state");

            List<AST<Node>> blocks = new List<AST<Node>>();
            List<AST<Node>> initStmts = new List<AST<Node>>();
            foreach (var stateName in allMachines[machineName].stateNameToStateInfo.Keys)
            {
                var stateInfo = allMachines[machineName].stateNameToStateInfo[stateName];
                var stateExpr = MkZingState(stateName);
                var condExpr = MkZingApply(ZingData.Cnst_Eq, state, stateExpr);
                string traceString = string.Format("\"<StateLog> Machine {0}-{{0}} entering State {1}\\n\"", machineName, stateInfo.printedName);
                List<AST<Node>> seqStmts = new List<AST<Node>>();
                seqStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance"))));
                seqStmts.Add(MkZingReturn(ZingData.Cnst_Nil));
                initStmts.Add(MkZingIfThen(condExpr, MkZingSeq(seqStmts)));
            }
            initStmts.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));
            blocks.Add(MkZingBlock("init", MkZingSeq(initStmts)));

            return MkZingMethodDecl("TraceEnterState", parameters, ZingData.Cnst_Void, MkZingVarDecls(locals), MkZingBlocks(blocks));
        }

        private AST<Node> GenerateTraceExitStateMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("state", SmState));
            List<AST<Node>> locals = new List<AST<Node>>();

            var state = MkZingIdentifier("state");

            List<AST<Node>> blocks = new List<AST<Node>>();
            List<AST<Node>> initStmts = new List<AST<Node>>();
            foreach (var stateName in allMachines[machineName].stateNameToStateInfo.Keys)
            {
                var stateInfo = allMachines[machineName].stateNameToStateInfo[stateName];
                var stateExpr = MkZingState(stateName);
                var condExpr = MkZingApply(ZingData.Cnst_Eq, state, stateExpr);
                string traceString = string.Format("\"<StateLog> Machine {0}-{{0}} exiting State {1}\\n\"", machineName, stateInfo.printedName);
                List<AST<Node>> seqStmts = new List<AST<Node>>();
                seqStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance"))));
                seqStmts.Add(MkZingReturn(ZingData.Cnst_Nil));
                initStmts.Add(MkZingIfThen(condExpr, MkZingSeq(seqStmts)));
            }
            initStmts.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));
            blocks.Add(MkZingBlock("init", MkZingSeq(initStmts)));

            return MkZingMethodDecl("TraceExitState", parameters, ZingData.Cnst_Void, MkZingVarDecls(locals), MkZingBlocks(blocks));
        }

        private AST<Node> GenerateCalculateDeferredAndActionSetMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("state", SmState));
            List<AST<Node>> locals = new List<AST<Node>>();

            var state = MkZingIdentifier("state");

            List<AST<Node>> blocks = new List<AST<Node>>();
            List<AST<Node>> initStmts = new List<AST<Node>>();
            foreach (var stateName in allMachines[machineName].stateNameToStateInfo.Keys)
            {
                var stateInfo = allMachines[machineName].stateNameToStateInfo[stateName];
                var stateExpr = MkZingState(stateName);
                var condExpr = MkZingApply(ZingData.Cnst_Eq, state, stateExpr);
                List<AST<Node>> seqStmts = new List<AST<Node>>();
                seqStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier(string.Format("{0}_CalculateDeferredAndActionSet", stateName)))));
                seqStmts.Add(MkZingReturn(ZingData.Cnst_Nil));
                initStmts.Add(MkZingIfThen(condExpr, MkZingSeq(seqStmts)));
            }
            initStmts.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));
            blocks.Add(MkZingBlock("init", MkZingSeq(initStmts)));

            return MkZingMethodDecl("CalculateDeferredAndActionSet", parameters, ZingData.Cnst_Void, MkZingVarDecls(locals), MkZingBlocks(blocks));
        }

        private AST<Node> GenerateRunHelperMethodDecl(string machineName)
        {
            AST<Node> parameters = MkZingVarDecls(MkZingVarDecl("start", ZingData.Cnst_Bool));
            List<AST<Node>> locals = new List<AST<Node>>();
            locals.Add(MkZingVarDecl("state", SmState));
            locals.Add(MkZingVarDecl("cont", Factory.Instance.MkCnst("Continuation")));
            locals.Add(MkZingVarDecl("transition", SmTransition));
            locals.Add(MkZingVarDecl("actionFun", Factory.Instance.MkCnst("ActionOrFun")));

            var start = MkZingIdentifier("start");
            var state = MkZingIdentifier("state");
            var cont = MkZingIdentifier("cont");
            var transition = MkZingIdentifier("transition");
            var actionFun = MkZingIdentifier("actionFun");

            List<AST<Node>> blocks = new List<AST<Node>>();

            List<AST<Node>> initStmts = new List<AST<Node>>();
            initStmts.Add(MkZingAssign(state, MkZingDot("myHandle", "stack", "state")));
            initStmts.Add(MkZingIfThenElse(start, MkZingGoto("enter"), MkZingGoto("handle")));
            blocks.Add(MkZingBlock("init", MkZingSeq(initStmts)));

            List<AST<Node>> enterStmts = new List<AST<Node>>();
            enterStmts.Add(MkZingAssign(state, MkZingDot("myHandle", "stack", "state")));
            enterStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("TraceEnterState"), state)));
            if (allMachines[machineName].IsMonitor)
            {
                if (compiler.Options.liveness == LivenessOption.None)
                {
                    enterStmts.Add(
                        MkZingIfThenElse(
                            MkZingEq(MkZingDot(state, "temperature"), MkZingDot("StateTemperature", "Hot")),
                            MkZingAssign(MkZingDot("SM_HANDLE", "hot"), MkZingAdd(MkZingDot("SM_HANDLE", "hot"), MkZingIdentifier("myHandle"))),
                            MkZingAssign(MkZingDot("SM_HANDLE", "hot"), MkZingSub(MkZingDot("SM_HANDLE", "hot"), MkZingIdentifier("myHandle")))));
                }
                else if (compiler.Options.liveness == LivenessOption.Standard)
                {
                    enterStmts.Add(
                        MkZingIfThenElse(
                            MkZingEq(MkZingDot(state, "temperature"), MkZingDot("StateTemperature", "Hot")),
                            MkZingAssign(MkZingDot("FairCycle", "temperature"), MkZingDot("StateTemperature", "Hot")),
                            MkZingIfThenElse(
                                MkZingEq(MkZingDot(state, "temperature"), MkZingDot("StateTemperature", "Warm")),
                                MkZingAssign(MkZingDot("FairCycle", "temperature"), MkZingDot("StateTemperature", "Warm")),
                                MkZingSeq(MkZingAssume(MkZingNeq(MkZingDot("FairCycle", "gate"), MkZingDot("GateStatus", "Closed"))),
                                          MkZingAssign(MkZingDot("FairCycle", "temperature"), MkZingDot("StateTemperature", "Cold"))))));
                }
                else
                {
                    // compiler.Options.liveness == LivenessOption.Mace
                    enterStmts.Add(
                        MkZingIfThenElse(
                            MkZingEq(MkZingDot(state, "temperature"), MkZingDot("StateTemperature", "Hot")),
                            MkZingCallStmt(MkZingCall(MkZingIdentifier("accept"), ZingData.Cnst_False)),
                            MkZingCallStmt(MkZingCall(MkZingIdentifier("accept"), ZingData.Cnst_True))));
                }
            }
            enterStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("CalculateDeferredAndActionSet"), state)));
            enterStmts.Add(MkZingAssign(actionFun, MkZingDot(state, "entryFun")));
            blocks.Add(MkZingBlock("enter", MkZingSeq(enterStmts)));

            List<AST<Node>> executeStmts = new List<AST<Node>>();
            executeStmts.Add(MkZingAssign(cont, MkZingCall(MkZingIdentifier("ReentrancyHelper"), actionFun)));
            executeStmts.Add(MkZingIfThen(MkZingEq(MkZingDot(cont, "reason"), MkZingDot("ContinuationReason", "Raise")), MkZingGoto("handle")));
            executeStmts.Add(MkZingAssign(MkZingDot("myHandle", "currentEvent"), MkZingIdentifier("null")));
            executeStmts.Add(MkZingAssign(MkZingDot("myHandle", "currentArg"), MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
            executeStmts.Add(MkZingIfThen(MkZingNeq(MkZingDot(cont, "reason"), MkZingDot("ContinuationReason", "Pop")), MkZingReturn(ZingData.Cnst_False)));
            executeStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("TraceExitState"), state)));
            executeStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("ReentrancyHelper"), MkZingDot(state, "exitFun"))));
            executeStmts.Add(MkZingReturn(ZingData.Cnst_True));
            blocks.Add(MkZingBlock("execute", MkZingSeq(executeStmts)));

            List<AST<Node>> seqStmts = new List<AST<Node>>();
            List<AST<Node>> handleStmts = new List<AST<Node>>();
            seqStmts.Add(MkZingAssign(actionFun, MkZingCall(MkZingDot("myHandle", "stack", "Find"), MkZingDot("myHandle", "currentEvent"))));
            seqStmts.Add(MkZingGoto("execute"));
            handleStmts.Add(MkZingIfThen(MkZingIn(MkZingDot("myHandle", "currentEvent"), MkZingDot("myHandle", "stack", "actionSet")), MkZingSeq(seqStmts)));
            handleStmts.Add(MkZingAssign(transition, MkZingCall(MkZingDot(state, "FindPushTransition"), MkZingDot("myHandle", "currentEvent"))));
            seqStmts = new List<AST<Node>>();
            seqStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("Run"), MkZingDot("transition", "to"))));
            seqStmts.Add(MkZingIfThenElse(MkZingEq(MkZingDot("myHandle", "currentEvent"), MkZingIdentifier("null")), MkZingReturn(ZingData.Cnst_False), MkZingGoto("handle")));
            handleStmts.Add(MkZingIfThen(MkZingNeq(transition, MkZingIdentifier("null")), MkZingSeq(seqStmts)));
            handleStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("TraceExitState"), state)));
            handleStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("ReentrancyHelper"), MkZingDot(state, "exitFun"))));
            handleStmts.Add(MkZingAssign(transition, MkZingCall(MkZingDot(state, "FindTransition"), MkZingDot("myHandle", "currentEvent"))));
            handleStmts.Add(MkZingIfThen(MkZingEq(transition, MkZingIdentifier("null")), MkZingReturn(ZingData.Cnst_True)));
            handleStmts.Add(MkZingCallStmt(MkZingCall(MkZingIdentifier("ReentrancyHelper"), MkZingDot("transition", "fun"))));
            handleStmts.Add(MkZingAssign(MkZingDot("myHandle", "stack", "state"), MkZingDot("transition", "to")));
            handleStmts.Add(MkZingAssign(state, MkZingDot("myHandle", "stack", "state")));
            handleStmts.Add(MkZingGoto("enter"));
            blocks.Add(MkZingBlock("handle", MkZingSeq(handleStmts)));

            return MkZingMethodDecl("RunHelper", parameters, ZingData.Cnst_Bool, MkZingVarDecls(locals), MkZingBlocks(blocks));
        }

        class ZingFoldContext
        {
            private PToZing pToZing;
            public string machineName;
            public string entityName;
            public FunInfo entityInfo;
            public Stack<List<AST<Node>>> sideEffectsStack;
            public List<Tuple<AST<Node>, string>> locals;
            public Stack<bool> lhsStack;
            private Dictionary<string, int> labels;
            public AST<Node> lastEval;

            public ZingFoldContext(PToZing comp, string machineName, string entityName, FunInfo entityInfo)
            {
                this.pToZing = comp;
                this.machineName = machineName;
                this.entityName = entityName;
                this.entityInfo = entityInfo;
                this.sideEffectsStack = new Stack<List<AST<Node>>>();
                PushSideEffectStack();
                this.locals = new List<Tuple<AST<Node>, string>>();
                this.labels = new Dictionary<string, int>();
                this.lhsStack = new Stack<bool>();
            }

            public int LabelToId(string l)
            {
                return labels[l];
            }

            public string GetFreshLabel()
            {
                var l = pToZing.GetUnique(entityName);
                labels[l] = labels.Count + 1;
                return l;
            }

            public AST<Node> EmitLabelPrelude()
            {
                var prelude = new List<AST<Node>>();
                var tmpVar = GetTmpVar(Factory.Instance.MkCnst("StackFrame"), "retTo");

                prelude.Add(PToZing.MkZingAssign(tmpVar, PToZing.MkZingCall(MkZingDot("entryCtxt", "PopReturnTo"))));
                prelude.Add(PToZing.MkZingAssign(MkZingIdentifier("vars"), MkZingDot(tmpVar, "vars")));
                prelude.Add(PToZing.MkZingIfThen(PToZing.MkZingEq(MkZingDot(tmpVar, "pc"), Factory.Instance.MkCnst(0)), MkZingGoto("start")));

                foreach (var l in labels.Keys)
                {
                    prelude.Add(PToZing.MkZingIfThen(PToZing.MkZingEq(MkZingDot(tmpVar, "pc"), Factory.Instance.MkCnst(labels[l])), MkZingGoto(l)));
                }

                prelude.Add(MkZingAssert(ZingData.Cnst_False, "Internal error"));

                return PToZing.MkZingSeq(prelude);
            }

            public void AddSideEffect(AST<Node> stmt)
            {
                this.sideEffectsStack.Peek().Add(stmt);
            }

            public void PushSideEffectStack()
            {
                this.sideEffectsStack.Push(new List<AST<Node>>());
            }

            // type must be Zing type
            public AST<Node> GetTmpVar(AST<Node> type, string baseName)
            {
                var tmpVarName = pToZing.GetUnique(baseName);
                var tmpVar = MkZingIdentifier(tmpVarName);
                this.locals.Add(new Tuple<AST<Node>, string>(type, tmpVarName));
                return tmpVar;
            }

            public AST<Node> EmitZingSideEffects(AST<Node> stmt)
            {
                Debug.Assert(sideEffectsStack.Count > 0);
                var sideEffects = sideEffectsStack.Pop();

                if (sideEffects.Count > 0)
                {
                    sideEffects.Add(stmt);
                    return PToZing.MkZingSeq(sideEffects);
                }
                else
                {
                    return stmt;
                }
            }

            public IEnumerable<AST<Node>> EmitLocals()
            {
                return locals.Select(loc => PToZing.MkZingVarDecl(loc.Item2, loc.Item1));
            }
        }

        private IEnumerable<Node> ZingUnfold(ZingFoldContext ctxt, Node n)
        {
            if (n.NodeKind != NodeKind.FuncTerm)
            {
                yield break;
            }

            var ft = (FuncTerm)n;
            var funName = ((Id)ft.Function).Name;
            if (funName == PData.Con_New.Node.Name || funName == PData.Con_NewStmt.Node.Name)
            {
                yield return GetArgByIndex(ft, 1);
            }
            else if (funName == PData.Con_FunApp.Node.Name || funName == PData.Con_FunStmt.Node.Name)
            {
                bool first = true;
                foreach (var t in ft.Args)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        foreach (var a in ZingUnfold(ctxt, t))
                        {
                            yield return a;
                        }
                    }
                }
            }
            else if (funName == PData.Con_BinApp.Node.Name)
            {
                var opName = ((Id)GetArgByIndex(ft, 0)).Name;
                if (opName == PData.Cnst_Idx.Node.Name && ctxt.lhsStack.Count > 0 && ctxt.lhsStack.Peek())
                {
                    ctxt.lhsStack.Push(true);
                    yield return GetArgByIndex(ft, 1);
                    ctxt.lhsStack.Pop();
                    ctxt.lhsStack.Push(false);
                    yield return GetArgByIndex(ft, 2);
                    ctxt.lhsStack.Pop();
                }
                else if (opName == PData.Cnst_And.Node.Name || opName == PData.Cnst_Or.Node.Name)
                {
                    ctxt.PushSideEffectStack();
                    yield return GetArgByIndex(ft, 1);
                    var left = ctxt.EmitZingSideEffects(ZingData.Cnst_Nil);
                    var leftEval = ctxt.lastEval;
                    ctxt.PushSideEffectStack();
                    yield return GetArgByIndex(ft, 2);
                    var right = ctxt.EmitZingSideEffects(ZingData.Cnst_Nil);
                    // short-circuit evaluation
                    if (opName == PData.Cnst_And.Node.Name)
                    {
                        ctxt.AddSideEffect(MkZingSeq(left, MkZingIfThenElse(MkZingDot(leftEval, "bl"), right, ZingData.Cnst_Nil)));
                    }
                    else
                    {
                        ctxt.AddSideEffect(MkZingSeq(left, MkZingIfThenElse(MkZingDot(leftEval, "bl"), ZingData.Cnst_Nil, right)));
                    }
                }
                else
                {
                    yield return GetArgByIndex(ft, 1);
                    yield return GetArgByIndex(ft, 2);
                }
            }
            else if (funName == PData.Con_Name.Node.Name ||
                     funName == PData.Con_NulApp.Node.Name ||
                     funName == PData.Con_UnApp.Node.Name ||
                     funName == PData.Con_Default.Node.Name ||
                     funName == PData.Con_Push.Node.Name ||
                     funName == PData.Con_Monitor.Node.Name ||
                     funName == PData.Con_NulStmt.Node.Name ||
                     funName == PData.Con_UnStmt.Node.Name)
            {
                var first = true;
                foreach (var t in ft.Args)
                {
                    if (first)
                    {
                        first = false;
                        continue;
                    }
                    yield return t;
                }
            }
            else if (funName == PData.Con_BinStmt.Node.Name)
            {
                var op = ((Id)GetArgByIndex(ft, 0)).Name;
                if (op == PData.Cnst_Assign.Node.Name)
                {
                    var lhs = (FuncTerm)GetArgByIndex(ft, 1);
                    var lhsName = ((Id)lhs.Function).Name;
                    if (lhsName == PData.Con_BinApp.Node.Name && ((Id)GetArgByIndex(lhs, 0)).Name == PData.Cnst_Idx.Node.Name)
                    {
                        ctxt.lhsStack.Push(true);
                        yield return GetArgByIndex(lhs, 1);
                        ctxt.lhsStack.Pop();
                        yield return GetArgByIndex(lhs, 2);
                    }
                    else if (lhsName == PData.Con_Field.Node.Name)
                    {
                        ctxt.lhsStack.Push(true);
                        yield return GetArgByIndex(lhs, 0);
                        ctxt.lhsStack.Pop();
                    }
                    else
                    {
                        ctxt.lhsStack.Push(true);
                        yield return lhs;
                        ctxt.lhsStack.Pop();
                    }
                    yield return GetArgByIndex(ft, 2);
                }
                else
                {
                    ctxt.lhsStack.Push(true);
                    yield return GetArgByIndex(ft, 1);
                    ctxt.lhsStack.Pop();
                    yield return GetArgByIndex(ft, 2);
                }
            }
            else if (funName == PData.Con_Field.Node.Name || funName == PData.Con_Cast.Node.Name)
            {
                yield return ft.Args.First<Node>();
            }
            else if (funName == PData.Con_Tuple.Node.Name)
            {
                foreach (var a in ZingUnfold(ctxt, GetArgByIndex(ft, 0)))
                {
                    yield return a;
                }
            }
            else if (funName == PData.Con_NamedTuple.Node.Name)
            {
                foreach (var a in ZingUnfold(ctxt, GetArgByIndex(ft, 0)))
                {
                    yield return a;
                }
            }
            else if (funName == PData.Con_Exprs.Node.Name)
            {
                do
                {
                    yield return ft.Args.First<Node>();
                    ft = GetArgByIndex(ft, 1) as FuncTerm;
                }
                while (ft != null);
            }
            else if (funName == PData.Con_NamedExprs.Node.Name)
            {
                do
                {
                    yield return GetArgByIndex(ft, 1);
                    ft = GetArgByIndex(ft, 2) as FuncTerm;
                }
                while (ft != null);
            }
            else if (funName == PData.Con_Seq.Node.Name)
            {
                using (var it = ft.Args.GetEnumerator())
                {
                    it.MoveNext();
                    yield return it.Current;
                    it.MoveNext();
                    ctxt.PushSideEffectStack();
                    yield return it.Current;
                }
            }
            else if (funName == PData.Con_Ite.Node.Name)
            {
                using (var it = ft.Args.GetEnumerator())
                {
                    it.MoveNext();
                    yield return it.Current;
                    it.MoveNext();
                    ctxt.PushSideEffectStack();
                    yield return it.Current;
                    it.MoveNext();
                    ctxt.PushSideEffectStack();
                    yield return it.Current;
                }
            }
            else if (funName == PData.Con_While.Node.Name)
            {
                using (var it = ft.Args.GetEnumerator())
                {
                    it.MoveNext();
                    ctxt.PushSideEffectStack();
                    yield return it.Current;
                    it.MoveNext();
                    ctxt.PushSideEffectStack();
                    yield return it.Current;
                }
            }
            else
            {
                foreach (var t in ft.Args)
                {
                    yield return t;
                }
            }
        }

        private AST<Node> MkZingAssignWithClone(AST<Node> lhs, AST<Node> rhs)
        {
            return MkZingAssign(lhs, MkZingCall(PrtCloneValue, rhs));
        }

        private ZingTranslationInfo ZingFold(ZingFoldContext ctxt, Node n, IEnumerable<ZingTranslationInfo> children)
        {
            if (n.NodeKind == NodeKind.Id || n.NodeKind == NodeKind.Cnst)
                return new ZingTranslationInfo(ZingData.Cnst_Nil);

            var ft = (FuncTerm)n;
            var funName = ((Id)ft.Function).Name;

            if (funName == PData.Con_Name.Node.Name)
            {
                return FoldName(ft, children, ctxt);
            }
            else if (funName == PData.Con_New.Node.Name)
            {
                return FoldNew(ft, children, ctxt);
            }
            else if (funName == PData.Con_FunApp.Node.Name)
            {
                return FoldFunApp(ft, children, ctxt);
            }
            else if (funName == PData.Con_NulApp.Node.Name)
            {
                return FoldNulApp(ft, children, ctxt);
            }
            else if (funName == PData.Con_UnApp.Node.Name)
            {
                return FoldUnApp(ft, children, ctxt);
            }
            else if (funName == PData.Con_BinApp.Node.Name)
            {
                return FoldBinApp(ft, children, ctxt);
            }
            else if (funName == PData.Con_Field.Node.Name)
            {
                return FoldField(ft, children, ctxt);
            }
            else if (funName == PData.Con_Default.Node.Name)
            {
                return FoldDefault(ft, children, ctxt);
            }
            else if (funName == PData.Con_Cast.Node.Name)
            {
                return FoldCast(ft, children, ctxt);
            }
            else if (funName == PData.Con_Tuple.Node.Name)
            {
                return FoldTuple(ft, children, ctxt);
            }
            else if (funName == PData.Con_NamedTuple.Node.Name)
            {
                return FoldNamedTuple(ft, children, ctxt);
            }
            else if (funName == PData.Con_Push.Node.Name)
            {
                return FoldPush(ft, children, ctxt);
            }
            else if (funName == PData.Con_NewStmt.Node.Name)
            {
                return FoldNewStmt(ft, children, ctxt);
            }
            else if (funName == PData.Con_Raise.Node.Name)
            {
                return FoldRaise(ft, children, ctxt);
            }
            else if (funName == PData.Con_Send.Node.Name)
            {
                return FoldSend(ft, children, ctxt);
            }
            else if (funName == PData.Con_Monitor.Node.Name)
            {
                return FoldMonitor(ft, children, ctxt);
            }
            else if (funName == PData.Con_FunStmt.Node.Name)
            {
                return FoldFunStmt(ft, children, ctxt);
            }
            else if (funName == PData.Con_NulStmt.Node.Name)
            {
                return FoldNulStmt(ft, children, ctxt);
            }
            else if (funName == PData.Con_UnStmt.Node.Name)
            {
                return FoldUnStmt(ft, children, ctxt);
            }
            else if (funName == PData.Con_BinStmt.Node.Name)
            {
                return FoldBinStmt(ft, children, ctxt);
            }
            else if (funName == PData.Con_Return.Node.Name)
            {
                return FoldReturn(ft, children, ctxt);
            }
            else if (funName == PData.Con_While.Node.Name)
            {
                return FoldWhile(ft, children, ctxt);
            }
            else if (funName == PData.Con_Ite.Node.Name)
            {
                return FoldIte(ft, children, ctxt);
            }
            else if (funName == PData.Con_Seq.Node.Name)
            {
                return FoldSeq(ft, children, ctxt);
            }
            else
            {
                Console.WriteLine("Unknown term name: " + funName);
                throw new NotImplementedException();
            }
        }

        ZingTranslationInfo FoldName(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            AST<Node> retVal;
            var name = GetName(ft, 0);
            FunInfo funInfo = ctxt.entityInfo as FunInfo;
            if (funInfo != null && funInfo.parameterNameToInfo.ContainsKey(name))
            {
                retVal = MkZingIndex(MkZingIdentifier("vars"), Factory.Instance.MkCnst(funInfo.parameterNameToInfo[name].index));
            }
            else if (ctxt.machineName != null && allMachines[ctxt.machineName].localVariableToVarInfo.ContainsKey(name))
            {
                retVal = MkZingIdentifier(name);
            }
            else
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeEvent.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetEvent"), tmpVar, MkZingEvent(name))));
                retVal = tmpVar;
            }
            ctxt.lastEval = retVal;
            return new ZingTranslationInfo(retVal);
        }

        ZingTranslationInfo FoldNew(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var typeName = GetName(ft, 2);
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                AST<Node> arg = it.Current.node;
                
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpSendPayload");
                if (arg == ZingData.Cnst_Nil)
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingAssignWithClone(tmpVar, arg));
                }

                AST<Node> retVal;
                MachineInfo machineInfo = allMachines[typeName];
                if (machineInfo.IsMonitor)
                {
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("Main", string.Format("CreateMachine_{0}", typeName)), tmpVar)));
                    retVal = ZingData.Cnst_Nil;
                }
                else
                {
                    var newMachine = ctxt.GetTmpVar(SmHandle, "newMachine");
                    ctxt.AddSideEffect(MkZingAssign(newMachine, MkZingCall(MkZingDot("Main", string.Format("CreateMachine_{0}", typeName)), tmpVar)));
                    string afterLabel = ctxt.GetFreshLabel();
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "NewMachine"), Factory.Instance.MkCnst(ctxt.LabelToId(afterLabel)), MkZingIdentifier("vars"), newMachine)));
                    ctxt.AddSideEffect(MkZingReturn(ZingData.Cnst_Nil));
                    ctxt.AddSideEffect(MkZingBlock(afterLabel, MkZingAssign(newMachine, MkZingDot("entryCtxt", "id"))));
                    ctxt.AddSideEffect(MkZingAssign(MkZingDot("entryCtxt", "id"), MkZingIdentifier("null")));
                    if (((Id)ft.Function).Name == "New")
                    {
                        var type = LookupType(ctxt, ft);
                        retVal = ctxt.GetTmpVar(PrtValue, "tmp");
                        ctxt.AddSideEffect(MkZingAssign(retVal, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(type))));
                        ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetMachine"), retVal, newMachine)));
                    }
                    else
                    {
                        // ((Id)ft.Function).Name == "NewStmt"
                        retVal = ZingData.Cnst_Nil;
                    }
                }
                ctxt.lastEval = retVal;
                return new ZingTranslationInfo(retVal);
            }
        }

        ZingTranslationInfo FoldFunApp(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var calleeName = GetName(ft, 0);
            var calleeInfo = allStaticFuns.ContainsKey(calleeName) ? allStaticFuns[calleeName] : allMachines[ctxt.machineName].funNameToFunInfo[calleeName];
            var argCloneVar = ctxt.GetTmpVar(Factory.Instance.MkCnst("PRT_VALUE_ARRAY"), "argCloneVar");
            ctxt.AddSideEffect(MkZingAssign(argCloneVar, MkZingNew(Factory.Instance.MkCnst("PRT_VALUE_ARRAY"), Factory.Instance.MkCnst(children.Count()))));
            int count = 0;
            foreach (var child in children)
            {
                var lhsInfo = calleeInfo.parameterNameToInfo[calleeInfo.parameterNames[count]];
                var rhsInfo = child;
                ctxt.AddSideEffect(MkZingAssignWithClone(MkZingIndex(argCloneVar, Factory.Instance.MkCnst(count)), child.node));
                count++;
            }
            
            foreach (var x in calleeInfo.invokeSchedulerFuns)
            {
                List<AST<Node>> invokeSchedulerArgs = new List<AST<Node>>();
                if (x.NodeKind == NodeKind.Cnst)
                {
                    Cnst cnst = x as Cnst;
                    if (cnst.CnstKind == CnstKind.String)
                    {
                        invokeSchedulerArgs.Add(Factory.Instance.MkCnst(string.Format("\"{0}\"", cnst.GetStringValue())));
                    }
                    else
                    {
                        invokeSchedulerArgs.Add(Factory.Instance.ToAST(x));
                    }
                }
                for (int i = 0; i < children.Count(); i++)
                {
                    invokeSchedulerArgs.Add(MkZingDot(MkZingIndex(argCloneVar, Factory.Instance.MkCnst(i)), "nt"));
                }
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingIdentifier("invokescheduler"), invokeSchedulerArgs)));
            }
            ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "PushReturnTo"), Factory.Instance.MkCnst(0), argCloneVar)));

            var beforeLabel = ctxt.GetFreshLabel();
            if (allStaticFuns.ContainsKey(calleeName))
            {
                ctxt.AddSideEffect(MkZingBlock(beforeLabel, MkZingCallStmt(MkZingCall(MkZingDot("Main", calleeName), MkZingIdentifier("myHandle"), MkZingIdentifier("entryCtxt")))));
            }
            else
            {
                ctxt.AddSideEffect(MkZingBlock(beforeLabel, MkZingCallStmt(MkZingCall(MkZingIdentifier(calleeName), MkZingIdentifier("entryCtxt")))));
            }

            AST<Node> processOutput;
            AST<Node> retVal;
            if (((Id)ft.Function).Name == "FunStmt")
            {
                retVal = ZingData.Cnst_Nil;
                processOutput = ZingData.Cnst_Nil;
            }
            else
            {
                retVal = ctxt.GetTmpVar(PrtValue, "ret");
                processOutput = MkZingAssign(retVal, MkZingDot("entryCtxt", "retVal"));
            } 
            ctxt.AddSideEffect(MkZingIfThenElse(
                                 MkZingEq(MkZingDot("entryCtxt", "reason"), MkZingDot("ContinuationReason", "Return")),
                                 processOutput,
                                 MkZingSeq(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "PushReturnTo"), Factory.Instance.MkCnst(ctxt.LabelToId(beforeLabel)), MkZingIdentifier("vars"))),
                                           MkZingReturn(ZingData.Cnst_Nil))));
            ctxt.lastEval = retVal;
            return new ZingTranslationInfo(retVal);
        }

        ZingTranslationInfo FoldNulApp(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var n = GetArgByIndex(ft, 0);
            if (n.NodeKind == NodeKind.Cnst)
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, Factory.Instance.ToAST(n))));
                ctxt.lastEval = tmpVar;
                return new ZingTranslationInfo(tmpVar);
            }

            // n.NodeKind == NodeKind.Id
            AST<Node> retVal;
            var op = ((Id)n).Name;
            if (op == PData.Cnst_True.Node.Name)
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, ZingData.Cnst_True)));
                retVal = tmpVar;
            }
            else if (op == PData.Cnst_False.Node.Name)
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, ZingData.Cnst_False)));
                retVal = tmpVar;
            }
            else if (op == PData.Cnst_This.Node.Name)
            {
                var machineType = PTypeReal;
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(machineType.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetMachine"), tmpVar, MkZingIdentifier("myHandle"))));
                retVal = tmpVar;
            }
            else if (op == PData.Cnst_Trigger.Node.Name)
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeEvent.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetEvent"), tmpVar, MkZingDot("myHandle", "currentEvent"))));
                retVal = tmpVar;
            }
            else if (op == PData.Cnst_Payload.Node.Name)
            {
                retVal = MkZingDot("myHandle", "currentArg");
            }
            else if (op == PData.Cnst_Nondet.Node.Name || op == PData.Cnst_FairNondet.Node.Name)
            {
                var afterLabel = ctxt.GetFreshLabel();
                var bvar = ctxt.GetTmpVar(ZingData.Cnst_Bool, "nondet");
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Nondet"), Factory.Instance.MkCnst(ctxt.LabelToId(afterLabel)), MkZingIdentifier("vars"))));
                ctxt.AddSideEffect(MkZingReturn(ZingData.Cnst_Nil));
                ctxt.AddSideEffect(MkZingBlock(afterLabel, MkZingAssign(bvar, MkZingDot("entryCtxt", "nondet"))));
                ctxt.AddSideEffect(MkZingAssign(MkZingDot("entryCtxt", "nondet"), ZingData.Cnst_False));
                if (compiler.Options.liveness == LivenessOption.Standard && op == PData.Cnst_FairNondet.Node.Name)
                {
                    int i = ctxt.entityInfo.numFairChoices;
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(GetFairChoice(ctxt.entityName, i), "AtChoose"), bvar)));
                    ctxt.entityInfo.numFairChoices++;
                }
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, bvar)));
                retVal = tmpVar;
            }
            else if (op == PData.Cnst_Null.Node.Name)
            {
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
                retVal = tmpVar;
            }
            else
            {
                // op == PData.Cnst_Halt.Node.Name
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmp");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeEvent.Node))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetEvent"), tmpVar, MkZingEvent(HaltEvent))));
                retVal = tmpVar;
            }
            ctxt.lastEval = retVal;
            return new ZingTranslationInfo(retVal);
        }

        ZingTranslationInfo FoldUnApp(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var op = ((Id)GetArgByIndex(ft, 0)).Name;
            using (var it = children.GetEnumerator())
            {
                AST<Node> retVal;
                it.MoveNext();
                var arg = it.Current.node;
                if (op == PData.Cnst_Not.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingNot(MkZingDot(arg, "bl")))));
                    retVal = tmpVar;
                }
                else if (op == PData.Cnst_Neg.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, MkZingApply(ZingData.Cnst_Neg, MkZingDot(arg, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Keys.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapGetKeys"), arg)));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Values.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapGetValues"), arg)));
                    retVal = tmpVar;
                }
                else
                {
                    //  op == PData.Cnst_Sizeof.Node.Name
                    var type = LookupType(ctxt, GetArgByIndex(ft, 1));
                    var typeOp = ((Id)type.Function).Name;
                    var tmpVar = ctxt.GetTmpVar(ZingData.Cnst_Int, "tmpVar");
                    if (typeOp == PData.Con_SeqType.Node.Name)
                    {
                        ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtSeqSizeOf"), arg)));
                    }
                    else
                    {
                        // typeOp == PData.Con_MapType.Node.Name)
                        ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapSizeOf"), arg)));
                    }
                    retVal = ctxt.GetTmpVar(PrtValue, "retVal");
                    ctxt.AddSideEffect(MkZingAssign(retVal, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), retVal, tmpVar)));
                }
                ctxt.lastEval = retVal;
                return new ZingTranslationInfo(retVal);
            }
        }

        ZingTranslationInfo FoldBinApp(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var op = ((Id)GetArgByIndex(ft, 0)).Name;
            using (var it = children.GetEnumerator())
            {
                AST<Node> retVal;
                it.MoveNext();
                var arg1 = it.Current.node;
                it.MoveNext();
                var arg2 = it.Current.node;

                if (op == PData.Cnst_Add.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, MkZingApply(ZingData.Cnst_Add, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar;
                }
                else if (op == PData.Cnst_Sub.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, MkZingApply(ZingData.Cnst_Sub, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Mul.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, MkZingApply(ZingData.Cnst_Mul, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_IntDiv.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeInt.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetInt"), tmpVar, MkZingApply(ZingData.Cnst_IntDiv, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar;
                }
                else if (op == PData.Cnst_And.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_And, MkZingDot(arg1, "bl"), MkZingDot(arg2, "bl")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Or.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_Or, MkZingDot(arg1, "bl"), MkZingDot(arg2, "bl")))));
                    retVal = tmpVar;
                }
                else if (op == PData.Cnst_Eq.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(ZingData.Cnst_Bool, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtIsEqualValue"), arg1, arg2)));
                    retVal = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(retVal, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), retVal, tmpVar)));
                }
                else if (op == PData.Cnst_NEq.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(ZingData.Cnst_Bool, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtIsEqualValue"), arg1, arg2)));
                    retVal = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(retVal, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), retVal, MkZingNot(tmpVar))));
                }
                else if (op == PData.Cnst_Lt.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_Lt, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Le.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_Le, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Gt.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_Gt, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Ge.Node.Name)
                {
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), tmpVar, MkZingApply(ZingData.Cnst_Ge, MkZingDot(arg1, "nt"), MkZingDot(arg2, "nt")))));
                    retVal = tmpVar; 
                }
                else if (op == PData.Cnst_Idx.Node.Name)
                {
                    var type = LookupType(ctxt, GetArgByIndex(ft, 1));
                    var typeOp = ((Id)type.Function).Name;
                    var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    if (typeOp == PData.Con_SeqType.Node.Name)
                    {
                        if (ctxt.lhsStack.Count > 0 && ctxt.lhsStack.Peek())
                        {
                            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtSeqGetNoClone"), arg1, arg2)));
                        }
                        else
                        {
                            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtSeqGet"), arg1, arg2)));
                        }
                    }
                    else
                    {
                        // op == PData.Con_MapType.Node.Name
                        if (ctxt.lhsStack.Count > 0 && ctxt.lhsStack.Peek())
                        {
                            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapGetNoClone"), arg1, arg2)));
                        }
                        else
                        {
                            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapGet"), arg1, arg2)));
                        }
                    }
                    retVal = tmpVar;
                }
                else
                {
                    // op == PData.Cnst_In.Node.Name
                    var tmpVar = ctxt.GetTmpVar(ZingData.Cnst_Bool, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtMapExists"), arg2, arg1)));
                    retVal = ctxt.GetTmpVar(PrtValue, "tmpVar");
                    ctxt.AddSideEffect(MkZingAssign(retVal, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeBool.Node))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtPrimSetBool"), retVal, tmpVar)));
                }
                ctxt.lastEval = retVal;
                return new ZingTranslationInfo(retVal);
            }
        }

        private int GetFieldIndex(string fieldName, FuncTerm nmdTupType)
        {
            int fieldIndex = 0;
            while (nmdTupType != null)
            {
                var fieldInfo = (FuncTerm)GetArgByIndex(nmdTupType, 0);
                var fieldNameInFieldInfo = (Cnst)GetArgByIndex(fieldInfo, 0);
                if (fieldName == fieldNameInFieldInfo.GetStringValue())
                    return fieldIndex;
                nmdTupType = GetArgByIndex(nmdTupType, 1) as FuncTerm;
                fieldIndex++;
            }
            Debug.Assert(false);
            return 0;
        }

        ZingTranslationInfo FoldField(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var expr = GetArgByIndex(ft, 0);
            var field = (Cnst)GetArgByIndex(ft, 1);
            int fieldIndex;
            if (field.CnstKind == CnstKind.Numeric)
            {
                fieldIndex = (int)field.GetNumericValue().Numerator;
            }
            else
            {
                fieldIndex = GetFieldIndex(field.GetStringValue(), LookupType(ctxt, expr));
            }
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var arg = it.Current.node;
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVal");
                if (ctxt.lhsStack.Count > 0 && ctxt.lhsStack.Peek())
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingIndex(MkZingDot(arg, "tuple"), Factory.Instance.MkCnst(fieldIndex))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(MkZingDot(PRT_VALUE, "PrtTupleGet"), arg, Factory.Instance.MkCnst(fieldIndex))));
                }
                ctxt.lastEval = tmpVar;
                return new ZingTranslationInfo(tmpVar);
            }
        }

        ZingTranslationInfo FoldDefault(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var typeArg = (FuncTerm)GetArgByIndex(ft, 0);
            var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(typeArg))));
            ctxt.lastEval = tmpVar;
            return new ZingTranslationInfo(tmpVar);
        }

        ZingTranslationInfo FoldCast(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var typeArg = (FuncTerm)GetArgByIndex(ft, 1);
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var valueArg = it.Current.node;
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpVar");
                ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtCastValue, valueArg, typeContext.PTypeToZingExpr(typeArg))));
                ctxt.lastEval = tmpVar;
                return new ZingTranslationInfo(tmpVar);
            }
        }

        ZingTranslationInfo FoldTuple(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var tupType = LookupType(ctxt, ft);
            var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpTuple");
            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(tupType))));
            int i = 0;
            foreach (var c in children)
            {
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtTupleSet"), tmpVar, Factory.Instance.MkCnst(i), c.node)));
                i++;
            }
            ctxt.lastEval = tmpVar;
            return new ZingTranslationInfo(tmpVar);
        }

        ZingTranslationInfo FoldNamedTuple(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var tupType = LookupType(ctxt, ft);
            var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpTuple");
            ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(tupType))));
            int i = 0;
            foreach (var c in children)
            {
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtTupleSet"), tmpVar, Factory.Instance.MkCnst(i), c.node)));
                i++;
            }
            ctxt.lastEval = tmpVar;
            return new ZingTranslationInfo(tmpVar);
        }

        ZingTranslationInfo FoldPush(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var qualifiedName = (FuncTerm)GetArgByIndex(ft, 0);
            var stateName = GetNameFromQualifiedName(ctxt.machineName, qualifiedName);
            var afterLabel = ctxt.GetFreshLabel();
            var res = MkZingSeq(
                MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Push"), Factory.Instance.MkCnst(ctxt.LabelToId(afterLabel)), MkZingIdentifier("vars"), MkZingState(stateName))),
                MkZingReturn(ZingData.Cnst_Nil),
                MkZingBlock(afterLabel, ZingData.Cnst_Nil));
            return new ZingTranslationInfo(res);
        }

        ZingTranslationInfo FoldNewStmt(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            return FoldNew(ft, children, ctxt);
        }

        ZingTranslationInfo FoldRaise(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var eventExpr = MkZingDot(it.Current.node, "ev");
                it.MoveNext();
                var payloadExpr = it.Current.node;
                var assertStmt = MkZingAssert(MkZingNeq(eventExpr, MkZingIdentifier("null")), string.Format("{0}: Raised event must be non-null", SpanToString(ft.Span)));
                string traceString = string.Format("\"<RaiseLog> Machine {0}-{{0}} raised Event {{1}}\\n\"", ctxt.machineName);
                var traceStmt = MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), MkZingDot("myHandle", "instance"), MkZingDot(eventExpr, "name")));
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpPayload");
                if (payloadExpr == ZingData.Cnst_Nil)
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingAssignWithClone(tmpVar, payloadExpr));
                }

                var assignStmt = MkZingSeq(MkZingAssign(MkZingDot("myHandle", "currentEvent"), eventExpr), MkZingAssign(MkZingDot("myHandle", "currentArg"), tmpVar));
                var createRetCtxt = MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Raise")));
                return new ZingTranslationInfo(MkZingSeq(assertStmt, traceStmt, assignStmt, createRetCtxt, MkZingReturn(ZingData.Cnst_Nil)));
            }
        }

        ZingTranslationInfo FoldSend(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var targetExpr = MkZingDot(it.Current.node, "mach");
                it.MoveNext();
                var eventExpr = MkZingDot(it.Current.node, "ev");
                it.MoveNext();
                AST<Node> payloadExpr = it.Current.node;
                ctxt.AddSideEffect(MkZingAssert(MkZingNeq(targetExpr, MkZingIdentifier("null")), "Target of send must be non-null"));
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpPayload");
                if (payloadExpr == ZingData.Cnst_Nil)
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingAssignWithClone(tmpVar, payloadExpr));
                }
                var afterLabel = ctxt.GetFreshLabel();
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot(targetExpr, "EnqueueEvent"), eventExpr, tmpVar, Factory.Instance.MkCnst("myHandle"))));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Send"), Factory.Instance.MkCnst(ctxt.LabelToId(afterLabel)), MkZingIdentifier("vars"))));
                return new ZingTranslationInfo(MkZingSeq(MkZingReturn(ZingData.Cnst_Nil), MkZingBlock(afterLabel, ZingData.Cnst_Nil)));
            }
        }

        ZingTranslationInfo FoldMonitor(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var typeName = GetName(ft, 0);
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                AST<Node> eventExpr = MkZingDot(it.Current.node, "ev");
                it.MoveNext();
                AST<Node> arg = it.Current.node;
                var tmpVar = ctxt.GetTmpVar(PrtValue, "tmpSendPayload");
                var assertStmt = MkZingAssert(MkZingNeq(eventExpr, MkZingIdentifier("null")), string.Format("{0}: Enqueued event must be non-null", SpanToString(ft.Span)));
                ctxt.AddSideEffect(assertStmt);
                if (arg == ZingData.Cnst_Nil)
                {
                    ctxt.AddSideEffect(MkZingAssign(tmpVar, MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(PTypeNull.Node))));
                    string traceString = 
                        string.Format("\"<MonitorLog> Enqueued Event < {{0}} > to {{1}} {0} monitors in Machine {1}-{{2}}\\n\"", typeName, ctxt.machineName);
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString), 
                        MkZingDot(eventExpr, "name"), MkZingCall(MkZingIdentifier("sizeof"), MkZingDot("Main", string.Format("{0}_handles", typeName))), MkZingDot("myHandle", "instance"))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingAssignWithClone(tmpVar, arg));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst("\"<MonitorLog> Enqueued Event < {0}, \""), MkZingDot(eventExpr, "name"))));
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "Print"), tmpVar)));
                    string traceString = string.Format("\" > to {{0}} {0} monitors in Machine {1}-{{1}}\\n\"", typeName, ctxt.machineName);
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(traceString),
                        MkZingCall(MkZingIdentifier("sizeof"), MkZingDot("Main", string.Format("{0}_handles", typeName))), MkZingDot("myHandle", "instance"))));
                }
                MachineInfo machineInfo = allMachines[typeName];
                return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("Main", string.Format("InvokeMachine_{0}", typeName)), eventExpr, tmpVar)));
            }
        }

        ZingTranslationInfo FoldFunStmt(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            return FoldFunApp(ft, children, ctxt);
        }

        ZingTranslationInfo FoldNulStmt(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var op = ((Id)GetArgByIndex(ft, 0)).Name;
            if (op == PData.Cnst_Pop.Node.Name)
            {
                ctxt.AddSideEffect(MkZingAssign(MkZingDot("myHandle", "currentEvent"), MkZingIdentifier("null")));
                ctxt.AddSideEffect(MkZingAssign(MkZingDot("myHandle", "currentArg"), MkZingIdentifier("null")));
                ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Pop"))));
                ctxt.AddSideEffect(MkZingReturn(ZingData.Cnst_Nil));
            }
            return new ZingTranslationInfo(ZingData.Cnst_Nil);

        }

        ZingTranslationInfo FoldUnStmt(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var op = ((Id)GetArgByIndex(ft, 0)).Name;
            // op == PData.Con_Assert.Node.Name
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                return new ZingTranslationInfo(MkZingAssert(MkZingDot(it.Current.node, "bl"), string.Format("{0}: Assert failed", SpanToString(ft.Span))));
            }
        }

        ZingTranslationInfo FoldBinStmt(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            var op = ((Id)GetArgByIndex(ft, 0)).Name;
            var lhs = (FuncTerm)GetArgByIndex(ft, 1);
            var type = LookupType(ctxt, lhs);
            var typeName = ((Id)type.Function).Name;
            AST<Node> src = null, index = null, dest = null;
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                dest = it.Current.node;
                it.MoveNext();
                src = it.Current.node;
                if (it.MoveNext())
                {
                    index = src;
                    src = it.Current.node;
                }

                if (op == PData.Cnst_Assign.Node.Name)
                {
                    if (((Id)lhs.Function).Name == PData.Con_Field.Node.Name)
                    {
                        var field = (Cnst)GetArgByIndex(lhs, 1);
                        int fieldIndex;
                        if (field.CnstKind == CnstKind.Numeric)
                        {
                            fieldIndex = (int)field.GetNumericValue().Numerator;
                        }
                        else
                        {
                            fieldIndex = GetFieldIndex(field.GetStringValue(), LookupType(ctxt, GetArgByIndex(lhs, 0)));
                        }
                        return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "PrtTupleSet"), dest, Factory.Instance.MkCnst(fieldIndex), src)));
                    }
                    else if (index == null)
                    {
                        return new ZingTranslationInfo(MkZingAssignWithClone(dest, src));
                    }
                    else
                    {
                        lhs = (FuncTerm)GetArgByIndex(lhs, 1);
                        type = LookupType(ctxt, lhs);
                        typeName = ((Id)type.Function).Name;
                        if (typeName == PData.Con_SeqType.Node.Name)
                        {
                            return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "PrtSeqSet"), dest, index, src)));
                        }
                        else
                        {
                            // type is PMapType
                            return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "PrtMapSet"), dest, index, src)));
                        }
                    }
                }
                else if (op == PData.Cnst_Remove.Node.Name)
                {
                    if (typeName == PData.Con_SeqType.Node.Name)
                    {
                        return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtSeqRemove"), dest, src)));
                    }
                    else
                    {
                        return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot(PRT_VALUE, "PrtMapRemove"), dest, src)));
                    }
                }
                else
                {
                    // op == PData.Cnst_Insert.Node.Name
                    var idx = ctxt.GetTmpVar(PrtValue, "idx");
                    var val = ctxt.GetTmpVar(PrtValue, "val");
                    ctxt.AddSideEffect(MkZingAssign(idx, MkZingCall(MkZingDot("PRT_VALUE", "PrtTupleGet"), src, Factory.Instance.MkCnst(0))));
                    ctxt.AddSideEffect(MkZingAssign(val, MkZingCall(MkZingDot("PRT_VALUE", "PrtTupleGet"), src, Factory.Instance.MkCnst(1))));
                    if (typeName == PData.Con_SeqType.Node.Name)
                    {
                        return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "PrtSeqInsert"), dest, idx, val)));
                    }
                    else
                    {
                        return new ZingTranslationInfo(MkZingCallStmt(MkZingCall(MkZingDot("PRT_VALUE", "PrtMapInsert"), dest, idx, val)));
                    }
                }
            }
        }

        ZingTranslationInfo FoldReturn(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            FunInfo funInfo = ctxt.entityInfo as FunInfo;
            AST<FuncTerm> returnType = PTypeNull;
            if (funInfo != null)
            {
                returnType = funInfo.returnType;
            }
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                if (returnType.Equals(PTypeNull))
                {
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Return"))));
                }
                else
                {
                    ctxt.AddSideEffect(MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "ReturnVal"), it.Current.node)));
                }
                ctxt.AddSideEffect(MkZingReturn(ZingData.Cnst_Nil));
                return new ZingTranslationInfo(ZingData.Cnst_Nil);
            }
        }

        ZingTranslationInfo FoldWhile(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var condExpr = MkZingDot(it.Current.node, "bl");
                it.MoveNext();
                var loopStart = GetUnique(ctxt.entityName + "_loop_start");
                var loopEnd = GetUnique(ctxt.entityName + "_loop_end");
                var body = it.Current.node;
                body = ctxt.EmitZingSideEffects(body);
                var res = MkZingBlock(loopStart, MkZingSeq(
                    ctxt.EmitZingSideEffects(MkZingIfThen(MkZingNot(condExpr), MkZingGoto(loopEnd))),
                    body,
                    MkZingGoto(loopStart),
                    MkZingBlock(loopEnd, ZingData.Cnst_Nil)));
                return new ZingTranslationInfo(res);
            }
        }

        ZingTranslationInfo FoldIte(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var condExpr = MkZingDot(it.Current.node, "bl");
                it.MoveNext();
                var thenStmt = it.Current.node;
                it.MoveNext();
                var elseStmt = it.Current.node;

                // Order in which we emit side effets (else,then) is the reverse of the order in which the side effect stacks were pushed(then, else).
                var ifName = GetUnique(ctxt.entityName + "_if");
                var elseLabel = ifName + "_else";
                var afterLabel = ifName + "_end";
                var cookedElse = MkZingBlock(elseLabel, ctxt.EmitZingSideEffects(elseStmt));
                var cookedThen = ctxt.EmitZingSideEffects(thenStmt);
                var res = MkZingSeq(MkZingIfThen(MkZingNot(condExpr), MkZingGoto(elseLabel)),
                    cookedThen,
                    MkZingGoto(afterLabel),
                    cookedElse,
                    MkZingBlock(afterLabel, ZingData.Cnst_Nil));
                return new ZingTranslationInfo(res);
            }
        }

        ZingTranslationInfo FoldSeq(FuncTerm ft, IEnumerable<ZingTranslationInfo> children, ZingFoldContext ctxt)
        {
            using (var it = children.GetEnumerator())
            {
                it.MoveNext();
                var first = it.Current;
                it.MoveNext();
                var second = it.Current;
                return new ZingTranslationInfo(MkZingSeq(first.node, ctxt.EmitZingSideEffects(second.node)));
            }
        }

        private AST<Node> MkZingStaticFunMethod(string funName, FunInfo funInfo)
        {
            List<AST<Node>> parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("myHandle", Factory.Instance.MkCnst("SM_HANDLE")));
            parameters.Add(MkZingVarDecl("entryCtxt", Factory.Instance.MkCnst("Continuation")));

            List<AST<Node>> localVars = new List<AST<Node>>();
            localVars.Add(MkZingVarDecl("vars", Factory.Instance.MkCnst("PRT_VALUE_ARRAY")));

            var ctxt = new ZingFoldContext(this, null, funName, funInfo);
            var tuple = Factory.Instance.ToAST(funInfo.body).Compute<ZingTranslationInfo>(
                x => ZingUnfold(ctxt, x),
                (x, ch) => ZingFold(ctxt, x, ch));
            Debug.Assert(ctxt.sideEffectsStack.Count == 1);
            var funBody = ctxt.EmitZingSideEffects(tuple.node);
            ctxt.PushSideEffectStack();
            funBody = MkZingSeq(
                ctxt.EmitLabelPrelude(),
                MkZingBlock("start", funBody),
                MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Return"))),
                MkZingReturn(ZingData.Cnst_Nil));
            return MkZingMethodDecl(funName, MkZingVarDecls(parameters), ZingData.Cnst_Void, MkZingVarDecls(localVars.Union(ctxt.EmitLocals())), MkZingBlocks(MkZingBlock("dummy", funBody)), ZingData.Cnst_Static);
        }

        private AST<Node> MkZingFunMethod(string machineName, string funName, FunInfo funInfo)
        {
            List<AST<Node>> parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("entryCtxt", Factory.Instance.MkCnst("Continuation")));

            List<AST<Node>> localVars = new List<AST<Node>>();
            localVars.Add(MkZingVarDecl("vars", Factory.Instance.MkCnst("PRT_VALUE_ARRAY")));

            var ctxt = new ZingFoldContext(this, machineName, funName, funInfo);
            var tuple = Factory.Instance.ToAST(funInfo.body).Compute<ZingTranslationInfo>(
                x => ZingUnfold(ctxt, x),
                (x, ch) => ZingFold(ctxt, x, ch));
            Debug.Assert(ctxt.sideEffectsStack.Count == 1);
            var funBody = ctxt.EmitZingSideEffects(tuple.node);
            ctxt.PushSideEffectStack();
            funBody = MkZingSeq(
                ctxt.EmitLabelPrelude(),
                MkZingBlock("start", funBody),
                MkZingCallStmt(MkZingCall(MkZingDot("entryCtxt", "Return"))),
                MkZingReturn(ZingData.Cnst_Nil));
            return MkZingMethodDecl(funName, MkZingVarDecls(parameters), ZingData.Cnst_Void, MkZingVarDecls(localVars.Union(ctxt.EmitLocals())), MkZingBlocks(MkZingBlock("dummy", funBody)));
        }

        private AST<Node> MkInvokeMonitorMethod(string machineName)
        {
            List<AST<Node>> parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("evt", Factory.Instance.MkCnst("SM_EVENT")));
            parameters.Add(MkZingVarDecl("arg", Factory.Instance.MkCnst("PRT_VALUE")));

            List<AST<Node>> localVars = new List<AST<Node>>();
            localVars.Add(MkZingVarDecl("doPop", ZingData.Cnst_Bool));

            var doPop = MkZingIdentifier("doPop");
            var machineInstance = MkZingIdentifier(string.Format("{0}_instance", machineName));
            var machineHandles = MkZingDot("Main", GetMonitorMachineName(machineName));
            string errorTraceString = string.Format("\"<StateLog> Unhandled event exception by machine {0}-{{0}}\\n\"", machineName);
            
            AST<Node> loopBody =
                MkZingSeq(
                    MkZingAssign(MkZingDot("iter", "myHandle", "currentEvent"), MkZingIdentifier("evt")),
                    MkZingAssign(MkZingDot("iter", "myHandle", "currentArg"), MkZingIdentifier("arg")),
                    MkZingAssign(doPop, MkZingCall(MkZingDot("iter", "RunHelper"), ZingData.Cnst_False)),
                    MkZingIfThen(doPop,
                                 MkZingSeq(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(errorTraceString), MkZingDot("iter", "myHandle", "instance"))),
                                           MkZingAssert(ZingData.Cnst_False))));
            AST<Node> body = MkZingBlock("dummy",
                                         AddArgs(ZingData.App_Foreach, Factory.Instance.MkCnst(ZingMachineClassName(machineName)), Factory.Instance.MkCnst("iter"), MkZingDot("Main", GetMonitorMachineName(machineName)), loopBody));
            return MkZingMethodDecl(string.Format("InvokeMachine_{0}", machineName), MkZingVarDecls(parameters), ZingData.Cnst_Void, MkZingVarDecls(localVars), MkZingBlocks(body), ZingData.Cnst_Static);
        }

        AST<Node> MkInitializers(string machineName, string obj)
        {
            List<AST<Node>> initializers = new List<AST<Node>>();
            var machineInfo = allMachines[machineName];
            foreach (string varName in machineInfo.localVariableToVarInfo.Keys)
            {
                var t = machineInfo.localVariableToVarInfo[varName].type;
                initializers.Add(MkZingAssign(MkZingDot(obj, varName), MkZingCall(PrtMkDefaultValue, typeContext.PTypeToZingExpr(t))));
            }
            return MkZingSeq(initializers);
        }

        private AST<Node> MkCreateMonitorMethod(string machineName)
        {
            var objectName = string.Format("o_{0}", machineName);
            var parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("arg", Factory.Instance.MkCnst("PRT_VALUE")));
            var localVars = new List<AST<Node>>();
            localVars.Add(MkZingVarDecl(objectName, Factory.Instance.MkCnst(ZingMachineClassName(machineName))));
            localVars.Add(MkZingVarDecl("chooseMonitor", ZingData.Cnst_Bool));
            localVars.Add(MkZingVarDecl("doPop", ZingData.Cnst_Bool));

            var machineInfo = allMachines[machineName];
            AST<Node> body = ZingData.Cnst_Nil;
            if (machineInfo.monitorType != MonitorType.SAFETY)
            {
                List<AST<Node>> stmts = new List<AST<Node>>();
                stmts.Add(MkZingIfThen(MkZingNeq(MkZingDot("FairCycle", "gate"), MkZingDot("GateStatus", "Init")), MkZingReturn(ZingData.Cnst_Nil)));
                stmts.Add(MkZingAssign(MkZingIdentifier("chooseMonitor"), MkZingCall(Factory.Instance.MkCnst("choose"), Factory.Instance.MkCnst("bool"))));
                stmts.Add(MkZingIfThen(MkZingIdentifier("chooseMonitor"), MkZingReturn(ZingData.Cnst_Nil)));
                if (machineInfo.monitorType == MonitorType.FINALLY)
                {
                    stmts.Add(MkZingAssign(MkZingDot("FairCycle", "gate"), MkZingDot("GateStatus", "Closed")));
                }
                else
                {
                    stmts.Add(MkZingAssign(MkZingDot("FairCycle", "gate"), MkZingDot("GateStatus", "Selected")));
                }
                body = MkZingSeq(stmts);
            }

            var doPop = MkZingIdentifier("doPop");
            var machineInstance = MkZingIdentifier(string.Format("{0}_instance", machineName));
            var machineHandles = MkZingDot("Main", GetMonitorMachineName(machineName));
            string createTraceString = string.Format("\"<CreateLog> Created Machine {0}-{{0}}\\n\"", machineName);
            string errorTraceString = string.Format("\"<StateLog> Unhandled event exception by machine {0}-{{0}}\\n\"", machineName);
            
            body = MkZingSeq(body,
                    MkZingAssign(MkZingIdentifier(objectName), MkZingNew(Factory.Instance.MkCnst(ZingMachineClassName(machineName)), ZingData.Cnst_Nil)),
                    MkInitializers(machineName, objectName),
                    MkZingAssign(MkZingDot(objectName, "myHandle"),
                                 MkZingCall(MkZingDot("SM_HANDLE", "Construct"), MkZingDot("Machine", string.Format("_{0}", machineName)), machineInstance, Factory.Instance.MkCnst(0))),
                    MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(createTraceString), machineInstance)),
                    MkZingAssign(MkZingDot(objectName, "myHandle", "currentArg"), MkZingIdentifier("arg")),
                    MkZingAssign(machineInstance, MkZingAdd(machineInstance, Factory.Instance.MkCnst(1))),
                    MkZingCallStmt(MkZingCall(MkZingDot(objectName, "myHandle", "Push"))),
                    MkZingAssign(MkZingDot(objectName, "myHandle", "stack", "state"), MkZingState(machineInfo.initStateName)),
                    MkZingAssign(machineHandles, MkZingAdd(machineHandles, MkZingIdentifier(objectName))),
                    MkZingAssign(doPop, MkZingCall(MkZingDot(objectName, "RunHelper"), ZingData.Cnst_True)),
                    MkZingIfThen(doPop, 
                                 MkZingSeq(MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(errorTraceString), MkZingDot(objectName, "myHandle", "instance"))),
                                           MkZingAssert(ZingData.Cnst_False)))
                    );
            body = MkZingBlock("dummy", body);

            return MkZingMethodDecl(string.Format("CreateMachine_{0}", machineName), MkZingVarDecls(parameters), ZingData.Cnst_Void, MkZingVarDecls(localVars), MkZingBlocks(body), ZingData.Cnst_Static);
        }

        private AST<Node> MkCreateMachineMethod(string machineName)
        {
            var objectName = string.Format("o_{0}", machineName);
            var parameters = new List<AST<Node>>();
            parameters.Add(MkZingVarDecl("arg", Factory.Instance.MkCnst("PRT_VALUE")));
            var localVars = new List<AST<Node>>();
            localVars.Add(MkZingVarDecl(objectName, Factory.Instance.MkCnst(ZingMachineClassName(machineName))));
            localVars.Add(MkZingVarDecl("fairScheduler", Factory.Instance.MkCnst("FairScheduler")));
            localVars.Add(MkZingVarDecl("fairChoice", Factory.Instance.MkCnst("FairChoice")));

            string createTraceString = string.Format("\"<CreateLog> Created Machine {0}-{{0}}\\n\"", machineName);
            var machineInstance = MkZingIdentifier(string.Format("{0}_instance", machineName));
            var body = MkZingSeq(
                    MkZingAssign(MkZingIdentifier(objectName), MkZingNew(Factory.Instance.MkCnst(ZingMachineClassName(machineName)), ZingData.Cnst_Nil)),
                    MkInitializers(machineName, objectName),
                    MkZingAssign(MkZingDot(objectName, "myHandle"),
                                 MkZingCall(MkZingDot("SM_HANDLE", "Construct"), MkZingDot("Machine", string.Format("_{0}", machineName)), machineInstance, Factory.Instance.MkCnst(allMachines[machineName].maxQueueSize))),
                    MkZingAssign(MkZingDot("SM_HANDLE", "enabled"), MkZingAdd(MkZingDot("SM_HANDLE", "enabled"), MkZingDot(objectName, "myHandle"))),
                    MkZingCallStmt(MkZingCall(MkZingIdentifier("trace"), Factory.Instance.MkCnst(createTraceString), machineInstance)),
                    MkZingAssign(MkZingDot(objectName, "myHandle", "currentArg"), MkZingIdentifier("arg")),
                    MkZingAssign(machineInstance, MkZingApply(ZingData.Cnst_Add, machineInstance, Factory.Instance.MkCnst(1)))
                    );

            if (compiler.Options.liveness == LivenessOption.Standard)
            {
                body = MkZingSeq(body,
                                 MkZingAssign(MkZingIdentifier("fairScheduler"), MkZingNew(Factory.Instance.MkCnst("FairScheduler"), ZingData.Cnst_Nil)),
                                 MkZingCallStmt(MkZingCall(MkZingDot("FairScheduler", "Init"), MkZingIdentifier("fairScheduler"), MkZingDot(objectName, "myHandle"))));
                foreach (var funName in allMachines[machineName].funNameToFunInfo.Keys)
                {
                    var funInfo = allMachines[machineName].funNameToFunInfo[funName];
                    for (int i = 0; i < funInfo.numFairChoices; i++)
                    {
                        body = MkZingSeq(body,
                                         MkZingAssign(MkZingIdentifier("fairChoice"), MkZingNew(Factory.Instance.MkCnst("FairChoice"), ZingData.Cnst_Nil)),
                                         MkZingCallStmt(MkZingCall(MkZingDot("FairChoice", "Init"), MkZingIdentifier("fairChoice"))),
                                         MkZingAssign(MkZingDot(objectName, GetFairChoice(funName, i)), MkZingIdentifier("fairChoice")));
                    }
                }
            }

            body = MkZingSeq(
                body,
                MkZingCallStmt(MkZingCall(MkZingDot(objectName, "Start")), ZingData.Cnst_Async),
                MkZingCallStmt(MkZingCall(MkZingIdentifier("invokescheduler"), Factory.Instance.MkCnst("\"map\""), MkZingDot(objectName, "myHandle", "machineId"))),
                MkZingReturn(MkZingDot(objectName, "myHandle"))
                );
            body = MkZingBlock("dummy", body);

            return MkZingMethodDecl(string.Format("CreateMachine_{0}", machineName), MkZingVarDecls(parameters), Factory.Instance.MkCnst("SM_HANDLE"), MkZingVarDecls(localVars), MkZingBlocks(body), ZingData.Cnst_Static);
        }

        private FuncTerm LookupType(ZingFoldContext ctxt, Node node)
        {
            return ctxt.entityInfo.typeInfo[Factory.Instance.ToAST(node)];
        }

        TypeTranslationContext typeContext;

        internal class TypeTranslationContext
        {
            private int fieldCount;
            private int typeCount;
            private List<AST<Node>> fieldNameInitialization;
            private List<AST<Node>> typeInitialization;
            private Dictionary<string, AST<FuncTerm>> fieldNameToZingExpr;
            private Dictionary<AST<Node>, AST<Node>> pTypeToZingExpr;

            public TypeTranslationContext()
            {
                fieldCount = 0;
                typeCount = 0;
                fieldNameInitialization = new List<AST<Node>>();
                typeInitialization = new List<AST<Node>>();
                fieldNameToZingExpr = new Dictionary<string, AST<FuncTerm>>();
                pTypeToZingExpr = new Dictionary<AST<Node>, AST<Node>>();
            }

            public AST<Node> InitializeFieldNamesAndTypes()
            {
                return MkZingSeq(MkZingSeq(fieldNameInitialization), MkZingSeq(typeInitialization));
            }

            public IEnumerable<AST<Node>> MainVarDecls()
            {
                List<AST<Node>> varDecls = new List<AST<Node>>();
                for (int i = 0; i < fieldCount; i++)
                {
                    varDecls.Add(MkZingVarDecl(string.Format("field_{0}_PRT_FIELD_NAME", i), Factory.Instance.MkCnst("PRT_FIELD_NAME"), ZingData.Cnst_Static));
                }
                for (int i = 0; i < typeCount; i++)
                {
                    varDecls.Add(MkZingVarDecl(string.Format("type_{0}_PRT_TYPE", i), Factory.Instance.MkCnst("PRT_TYPE"), ZingData.Cnst_Static));
                }
                return varDecls;
            }

            private AST<FuncTerm> GetField(string fieldName)
            {
                if (fieldNameToZingExpr.ContainsKey(fieldName))
                    return fieldNameToZingExpr[fieldName];
                var retVal = MkZingDot("Main", string.Format("field_{0}_PRT_FIELD_NAME", fieldCount));
                AddFieldNameInitialization(MkZingAssign(retVal, MkZingNew(Factory.Instance.MkCnst("PRT_FIELD_NAME"), ZingData.Cnst_Nil)));
                fieldCount++;
                fieldNameToZingExpr[fieldName] = retVal;
                return retVal;
            }

            private new AST<FuncTerm> GetType()
            {
                var retVal = MkZingDot("Main", string.Format("type_{0}_PRT_TYPE", typeCount));
                typeCount++;
                return retVal;
            }

            private void AddFieldNameInitialization(AST<Node> n)
            {
                fieldNameInitialization.Add(n);
            }


            private void AddTypeInitialization(AST<Node> n)
            {
                typeInitialization.Add(n);
            }

            public AST<Node> PTypeToZingExpr(FuncTerm pType)
            {
                var pTypeAST = Factory.Instance.ToAST(pType);
                if (!pTypeToZingExpr.ContainsKey(pTypeAST))
                {
                    pTypeToZingExpr[pTypeAST] = ConstructType(pType);
                }
                return pTypeToZingExpr[pTypeAST];
            }

            private AST<Node> ConstructType(FuncTerm type)
            {
                string typeKind = ((Id)type.Function).Name;
                if (typeKind == "BaseType")
                {
                    var primitiveType = ((Id)GetArgByIndex(type, 0)).Name;
                    if (primitiveType == "NULL")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_NULL"))));
                        return tmpVar;
                    }
                    else if (primitiveType == "BOOL")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_BOOL"))));
                        return tmpVar;
                    }
                    else if (primitiveType == "INT")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_INT"))));
                        return tmpVar;
                    }
                    else if (primitiveType == "EVENT")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_EVENT"))));
                        return tmpVar;
                    }
                    else if (primitiveType == "REAL")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_REAL"))));
                        return tmpVar;
                    }
                    else if (primitiveType == "ANY")
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_ANY"))));
                        return tmpVar;
                    }
                    else
                    {
                        var tmpVar = GetType();
                        AddTypeInitialization(MkZingAssign(tmpVar, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkPrimitiveType"), MkZingDot("PRT_TYPE_KIND", "PRT_KIND_FORGN"))));
                        return tmpVar;
                    }
                }
                else if (typeKind == "TupType")
                {
                    List<AST<Node>> memberTypes = new List<AST<Node>>();
                    while (type != null)
                    {
                        memberTypes.Add(PTypeToZingExpr((FuncTerm)GetArgByIndex(type, 0)));
                        type = GetArgByIndex(type, 1) as FuncTerm;
                    }
                    var tupleType = GetType();
                    AddTypeInitialization(MkZingAssign(tupleType, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkTupType"), Factory.Instance.MkCnst(memberTypes.Count))));
                    for (int i = 0; i < memberTypes.Count; i++)
                    {
                        AddTypeInitialization(MkZingCallStmt(MkZingCall(MkZingDot("PRT_TYPE", "PrtSetFieldType"), tupleType, Factory.Instance.MkCnst(i), memberTypes[i])));
                    }
                    return tupleType;
                }
                else if (typeKind == "NmdTupType")
                {
                    List<AST<Node>> memberNames = new List<AST<Node>>();
                    List<AST<Node>> memberTypes = new List<AST<Node>>();
                    while (type != null)
                    {
                        var typeField = (FuncTerm)GetArgByIndex(type, 0);
                        memberNames.Add(GetField(((Cnst)GetArgByIndex(typeField, 0)).GetStringValue()));
                        memberTypes.Add(PTypeToZingExpr((FuncTerm)GetArgByIndex(typeField, 1)));
                        type = GetArgByIndex(type, 1) as FuncTerm;
                    }
                    var tupleType = GetType();
                    AddTypeInitialization(MkZingAssign(tupleType, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkNmdTupType"), Factory.Instance.MkCnst(memberTypes.Count))));
                    for (int i = 0; i < memberTypes.Count; i++)
                    {
                        AddTypeInitialization(MkZingCallStmt(MkZingCall(MkZingDot("PRT_TYPE", "PrtSetFieldName"), tupleType, Factory.Instance.MkCnst(i), memberNames[i])));
                        AddTypeInitialization(MkZingCallStmt(MkZingCall(MkZingDot("PRT_TYPE", "PrtSetFieldType"), tupleType, Factory.Instance.MkCnst(i), memberTypes[i])));
                    }
                    return tupleType;
                }
                else if (typeKind == "SeqType")
                {
                    var innerType = PTypeToZingExpr((FuncTerm)GetArgByIndex(type, 0));
                    var seqType = GetType();
                    AddTypeInitialization(MkZingAssign(seqType, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkSeqType"), innerType)));
                    return seqType;
                }
                else
                {
                    // typeKind == "MapType"
                    var domType = PTypeToZingExpr((FuncTerm)GetArgByIndex(type, 0));
                    var codType = PTypeToZingExpr((FuncTerm)GetArgByIndex(type, 1));
                    var mapType = GetType();
                    AddTypeInitialization(MkZingAssign(mapType, MkZingCall(MkZingDot("PRT_TYPE", "PrtMkMapType"), domType, codType)));
                    return mapType;
                }
            }
        }
        #endregion
    }
}
