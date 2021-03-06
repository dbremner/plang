#include "PrtDist.h"


void
PrtDistSMExceptionHandler(
__in PRT_STATUS exception,
__in void* vcontext
)
{

	PRT_MACHINEINST *context = (PRT_MACHINEINST*)vcontext;
	PRT_STRING MachineName = context->process->program->machines[context->instanceOf].name;
	PRT_UINT32 MachineId = context->id->valueUnion.mid->machineId;


	FILE *logFile;
	PRT_MACHINEINST_PRIV *c = (PRT_MACHINEINST_PRIV*)vcontext;

	PrtLockMutex(((PRT_PROCESS_PRIV*)c->process)->processLock);
	PRT_CHAR fileName[100] = "PRT_CONTAINER_LOG_";
	PRT_CHAR processId[100];
	_itoa(c->id->valueUnion.mid->processId.data1, processId, 10);
	strcat_s(fileName, 100, processId);
	strcat_s(fileName, 100, ".txt");
	logFile = fopen(fileName, "a+");
	PRT_CHAR log[100];

	switch (exception)
	{
	case PRT_STATUS_EVENT_UNHANDLED:
		sprintf(log,
			"<EXCEPTION> Machine %s(%d) : Unhandled Event Exception\n",
			MachineName,
			MachineId);
		break;
	case PRT_STATUS_EVENT_OVERFLOW:
		sprintf(log,
			"<EXCEPTION> Machine %s(%d) : MaxInstance of Event Exceeded Exception\n",
			MachineName,
			MachineId);
		break;
	case PRT_STATUS_QUEUE_OVERFLOW:
		sprintf(log,
			"<EXCEPTION> Queue Size Exceeded Max Limits in Machine %s(%d)\n",
			MachineName,
			MachineId);
		break;
	case PRT_STATUS_ILLEGAL_SEND:
		sprintf(log,
			"<EXCEPTION> Machine %s(%d) : Illegal use of send for sending message across process (source and target machines are in different process) ", 
			MachineName,
			MachineId);
		break;
	default:
		sprintf(log,
			"<EXCEPTION> Machine %s(%d) : Unknown Exception\n",
			MachineName,
			MachineId);
		break;
	}

	fputs(log, logFile);
	fflush(logFile);
	PrtUnlockMutex(((PRT_PROCESS_PRIV*)c->process)->processLock);

#ifdef PRT_DEBUG
	int msgboxID = MessageBoxEx(
		NULL,
		log,
		fileName,
		MB_OK,
		LANG_NEUTRAL
		);

	switch (msgboxID)
	{
	case IDOK:
		exit(0);
	default:
		exit(-1);
	}
#endif
	exit(-1);

}

void PrtDistSMLogHandler(PRT_STEP step, void *vcontext)
{
	static FILE *logfile = NULL;
	PRT_MACHINEINST_PRIV *c = (PRT_MACHINEINST_PRIV*)vcontext;
	PrtLockMutex(((PRT_PROCESS_PRIV*)ContainerProcess)->processLock);
	if (logfile == NULL)
	{
		PRT_CHAR fileName[100] = "PRT_CONTAINER_LOG_";
		PRT_CHAR processId[100];
		_itoa(ContainerProcess->guid.data1, processId, 10);
		strcat_s(fileName, 100, processId);
		strcat_s(fileName, 100, ".txt");
		logfile = fopen(fileName, "a+");
	}

	PRT_STRING log = NULL;
	if (step == PRT_STEP_COUNT) //special logging
	{
		log = (PRT_STRING)vcontext;
	}
	else
	{
		log = PrtToStringStep(step, vcontext);
	}
	fputs(log, logfile);
	fputs("\n", logfile);
	fflush(logfile);
	PrtUnlockMutex(((PRT_PROCESS_PRIV*)ContainerProcess)->processLock);
}


