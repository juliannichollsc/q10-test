import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { ordersQueryKey } from "./api";

export function useOrderUpdates() {
  const queryClient = useQueryClient();
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let active = true;
    const update = (value: boolean) => active && setConnected(value);
    const refresh = () => queryClient.invalidateQueries({ queryKey: ordersQueryKey });

    const connection = new HubConnectionBuilder()
      .withUrl("/api/hubs/orders")
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    connection.on("OrderChanged", refresh);
    connection.onreconnecting(() => update(false));
    connection.onreconnected(() => {
      update(true);
      void refresh();
    });
    connection.onclose(() => update(false));
    connection.start().then(
      () => update(true),
      () => update(false),
    );

    return () => {
      active = false;
      void connection.stop();
    };
  }, [queryClient]);

  return connected;
}
