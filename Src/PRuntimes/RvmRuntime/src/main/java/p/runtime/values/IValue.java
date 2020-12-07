/* Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved. */ 
package p.runtime.values;

import java.io.Serializable;

// Interface for values.
public interface IValue<T extends IValue<T>> extends Serializable {
    public T genericClone();

    public static <S extends IValue<S>> S safeClone(IValue<S> value) {
        if (value == null) {
            return null;
        }
        return value.genericClone();
    }

    public static boolean safeEquals(IValue<?> first, IValue<?> second) {
        if (first == null) {
            return second == null;
        }
        return first.equals(second);
    }
}
