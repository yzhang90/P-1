package mop;

import java.util.logging.*;

public class RVMLogger {

    Logger logger;

    public RVMLogger(String logPath) {
        try {
            boolean append = false;
            FileHandler handler = new FileHandler(logPath, append);
            logger = Logger.getLogger("RVMLogger");
            logger.setUseParentHandlers(false);
            logger.addHandler(handler);
            handler.setFormatter(new MyCustomFormatter());
        } catch (Exception e) {
            System.out.println("RVMLogger creation failed.");
        }
    }

    public void log(String msg) {
        logger.info(msg);
    }

    private static class MyCustomFormatter extends Formatter {

        @Override
        public String format(LogRecord record) {
            StringBuffer sb = new StringBuffer();
            sb.append(record.getMessage());
            sb.append("\n");
            return sb.toString();
        }

    }
}
