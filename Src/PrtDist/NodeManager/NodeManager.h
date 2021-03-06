#include<string>
#include<iostream>
#include <windows.h>
#include <thread>
#include<fstream>
#include <mutex>
#include <chrono>
#include"NodeManager_h.h"
#include"NodeManager_s.c"
#include "ConfigParser.h"
using namespace std;



/* GLobal Variables */
char* logFileName = "PRTDIST_NODEMANAGER.txt";
int CurrentNodeID = 0;
int myNodeId;
FILE* logFile;
std::mutex g_lock;

struct ClusterConfig ClusterConfiguration;

void PrtDistServiceCreateLogFile();
void PrtDistServiceCloseLogFile();
void PrtDistServiceLog(char* log);

string PrtDistServiceNextNodeManagerPort();
void PrtDistServiceCreateRPCServer();

//Helper functions used across PrtDistManager projects.
boolean _ROBOCOPY(string source, string dest);

//For concatenating two strings
void _CONCAT(char* dest, char* string1, char* string2);