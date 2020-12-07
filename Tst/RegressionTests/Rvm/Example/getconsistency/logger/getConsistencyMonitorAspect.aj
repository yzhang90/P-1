package mop;

import java.lang.ref.*;
import org.aspectj.lang.*;

// add your own imports.
import db.*;


aspect BaseAspect {
    pointcut notwithin() :
        !within(sun..*) &&
        !within(java..*) &&
        !within(javax..*) &&
        !within(com.sun..*) &&
        !within(org.apache.commons..*) &&
        !within(org.apache.geronimo..*) &&
        !within(net.sf.cglib..*) &&
        !within(mop..*) &&
        !within(javamoprt..*) &&
        !within(rvmonitorrt..*) &&
        !within(com.runtimeverification..*);
}

// Signatures of all the events that need dispatching.
// getConsistencyRuntimeMonitor.getConsistency_eGetReqEvent(NamedTuple)
// getConsistencyRuntimeMonitor.getConsistency_eGetRespEvent(NamedTuple)
// getConsistencyRuntimeMonitor.getConsistency_ePutReqEvent(NamedTuple)
// getConsistencyRuntimeMonitor.getConsistency_ePutRespEvent(NamedTuple)

public aspect getConsistencyMonitorAspect implements com.runtimeverification.rvmonitor.java.rt.RVMObject {
    RVMLogger logger = new RVMLogger("/home/zy/getConsistency.log");

    getConsistencyMonitorAspect() { }
    
    pointcut MOP_CommonPointCut() : !within(com.runtimeverification.rvmonitor.java.rt.RVMObject+) && !adviceexecution() && BaseAspect.notwithin();
    
    // Implement your code here.
    pointcut getConsistency_getReq(String key, int rId) : (execution(* Database.getReq(String, int)) && args(key, rId)) && MOP_CommonPointCut();
    after (String key, int rId) : getConsistency_getReq(key, rId) {
        logger.log(String.format("getReq(%s, %d)", key, rId));
    }

    pointcut getConsistency_getRes(boolean res, Record r, int rId) : (execution(* Database.getRes(boolean, Record, int)) && args(res, r, rId)) && MOP_CommonPointCut();
    after (boolean res, Record r, int rId) : getConsistency_getRes(res, r, rId) {
        String recordStr = String.format("Record(%s, %d, %d)", r.key, r.val, r.sqr);
        logger.log(String.format("getRes(%b, %s, %d)", res, recordStr, rId));
    }

    pointcut getConsistency_putReq(Record r, int rId) : (execution(* Database.putReq(Record, int)) && args(r, rId)) && MOP_CommonPointCut();
    after (Record r, int rId) : getConsistency_putReq(r, rId) {
        String recordStr = String.format("Record(%s, %d, %d)", r.key, r.val, r.sqr);
        logger.log(String.format("putReq(%s, %d)", recordStr, rId));
    }

    pointcut getConsistency_putRes(boolean res, Record r, int rId) : (execution(* Database.putRes(boolean, Record, int)) && args(res, r, rId)) && MOP_CommonPointCut();
    after (boolean res, Record r, int rId) : getConsistency_putRes(res, r, rId) {
        String recordStr = String.format("Record(%s, %d, %d)", r.key, r.val, r.sqr);
        logger.log(String.format("putRes(%b, %s, %d)", res, recordStr, rId));
    }
}
