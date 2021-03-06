//Model function with multiple sends + new + nondet operation in it
//Model functions are in real machine

event nondeteventT:bool;
event nondeteventF:bool;
event done;

machine Real {
var ghostm :mid;
var setme : int;
start state init {
		 entry { setme = (int)payload; assert(setme == 1);
		}
		on nondeteventT goto nextstate;
		on nondeteventF goto nextstate;
	}
	state nextstate {
		entry {
			assert(trigger == nondeteventT && payload == true || trigger == nondeteventF && payload == false);
			setme = setme + 1;
		}
		on nondeteventT goto nnextstate;
		on nondeteventF goto nnextstate; 
	}
	state nnextstate {
		entry {
			assert(trigger == nondeteventT && payload == false || trigger == nondeteventF && payload == true);
		}
	}
}

main machine Ghost {
    var local:int;
	var real : id;
	model fun createMachine()
	{	
		real = new Real(1);
	}
	
	model fun nondetsend() 
	{
		if(*)
		{
			send(real, nondeteventT, true);
			send(real, nondeteventT, false);
		}
		else
		{
			send(real, nondeteventF, false);
			send(real, nondeteventF, true);
		}
	}
	
    start state Ghost_Init {
        
		entry {
			createMachine();
			nondetsend();
			raise(done);
        }
		on done goto endstate;
    }
	
	state endstate {
		entry {
		}
	}
}
