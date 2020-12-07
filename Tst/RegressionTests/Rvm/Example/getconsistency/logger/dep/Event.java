package mop;

import java.util.*;
import p.runtime.values.*;

public class Event {

    private String eventName;
    private List<IValue> args;

    public Event(String name, List<IValue> args) {
        this.eventName = name;
        this.args = args;
    }

    public String getEventName() {
        return this.eventName;
    }

    public List<IValue> getArgs() {
        return this.args;
    }
}
