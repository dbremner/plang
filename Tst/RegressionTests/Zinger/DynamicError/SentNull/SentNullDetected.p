// P semantics test: two machines, "send", "raise", monitor invocation for non-constant event expressions

event E1 assert 1;
event E2 assert 1: bool;

main machine Real1 {
    var test: bool; 
	var mac: machine;
	var ev1: event;
	var ev2: event;
	var ev3: int;
    start state Real1_Init {
        entry { 
			mac = new Real2(this);
			//ev2 = E2;
            send mac, ev2, test;	 
			raise ev1;   
			monitor M, ev1; 
				
        } 	
        on E1 do Action1;   
		
		//on E2 do Action2;
        exit {   }
	}
    fun Action1() {
		test = true;
    }
	
}
machine Real2 {
	var pl: bool;
	start state Real2_Init {
		entry {		
		}
		on E2 do {
			if (trigger == E2) 
			{ 
				Action2(); 
			}
			else 
			{ assert(false);};  //why is this reachable?
		};
	}
	fun Action2() {
		assert(payload == false);  //should fail
    }
}
monitor M {
	start state x {
	}
}