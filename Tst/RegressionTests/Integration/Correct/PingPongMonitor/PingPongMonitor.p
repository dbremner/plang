event Ping assert 1 : machine;
event Pong assert 1;
event Success;

main machine PING {
    var pongId: machine;

    start state Ping_Init {
        entry {
	      new M();
  	    pongId = new PONG();
	    raise Success;   	   
        }
        on Success goto Ping_SendPing;
    }

    state Ping_SendPing {
        entry {
	      monitor M, Ping, this;
	    send pongId, Ping, this;
	    raise Success;
	}
        on Success goto Ping_WaitPong;
     }

     state Ping_WaitPong {
        on Pong goto Ping_SendPing;
     }

    state Done { }
}

machine PONG {
    start state Pong_WaitPing {
        entry { }
        on Ping goto Pong_SendPong;
    }

    state Pong_SendPong {
	entry {
	      monitor M, Pong;
	     send payload as machine, Pong;
	     raise Success;		 	  
	}
        on Success goto Pong_WaitPing;
    }
}

monitor M {
start cold state ExpectPing {
		 on Ping goto ExpectPong;
}

hot state ExpectPong {
		 on Pong goto ExpectPing;
}
}
