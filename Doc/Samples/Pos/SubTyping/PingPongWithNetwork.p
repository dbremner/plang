event Ping:id assert 1;
event Pong assert 1;
event Forward:(to: id, ev: eid, val: any) assert 1;
event Success;
event Bye;

main machine PING {
    var pongId: id;
    var ctr: int;
    var networkId: mid;
    start state Ping_Init {
        entry {
            ctr = 0;
            networkId = new Network();
  	    pongId = new PONG(networkId);
	    raise (Success);   	   
        }
        on Success goto Ping_SendPing;
    }

    state Ping_SendPing {
        entry {
          if (ctr > 2) raise (Bye);
	      send (networkId, Forward, (to=pongId, ev=Ping, val=this));
	    raise (Success);
	}
        on Success goto Ping_WaitPong;
        on Bye goto Done;
     }

     state Ping_WaitPong {
	entry { ctr = ctr + 1; }
        on Pong goto Ping_SendPing;
     }

    state Done { }
}

machine PONG {
    var networkId: mid;

    start state _init {
	entry { networkId = (mid)payload; raise (Success); }
        on Success goto Pong_WaitPing;
    }

    state Pong_WaitPing {
        entry { }
        on Ping goto Pong_SendPong;
    }

    state Pong_SendPong {
	entry {
	      send (networkId, Forward, (to=(id) payload, ev=Pong, val=null));
	     raise (Success);		 	  
	}
        on Success goto Pong_WaitPing;
    }
}

model machine Network {
    var x:(to: id, ev: eid, val: any);
    start state Wait {
        on Forward goto Send;
    }
    state Send {
        entry {
	      x = ((to: id, ev: eid, val: any)) payload;
	      send(x.to, x.ev, x.val);
	      raise (Success);
        }
        on Success goto Wait;
    }
}
