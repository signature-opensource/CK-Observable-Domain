import { WatchEvent } from '@signature-code/ck-observable-domain';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { IObservableDomainLeagueDriver } from '@signature-code/ck-observable-domain/src/iod-league-driver';
import { ICrisEndpoint, SignalRObservableWatcherStartOrRestartCommand, VESACode } from "@signature/generated";

export class SignalRObservableLeagueDomainService implements IObservableDomainLeagueDriver {
  private readonly connection: HubConnection;
  constructor(hubUrl: string, private readonly crisEndpoint: ICrisEndpoint) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, HttpTransportType.None)
      .build();
  }

  public async start(): Promise<boolean> {
    try {
      await this.connection.start();
      return true;
    } catch (e) {
      console.log(e);
      return false;
    }
  }

  public async startListening(domainsNames: { domainName: string, transactionCount: number }[]): Promise<{ [domainName: string]: WatchEvent }> {
    const res: { [domainName: string]: WatchEvent; } = {};
    const promises = domainsNames.map(async element => {
      const poco = SignalRObservableWatcherStartOrRestartCommand.create(element.domainName, element.transactionCount);
      const resp = await this.crisEndpoint.send(poco);
      if (resp.code == 'CommunicationError') throw resp.result;
      if (resp.code != VESACode.Synchronous) throw new Error(JSON.stringify(resp.result));
      res[element.domainName] = JSON.parse(resp.result!);
    });
    await Promise.all(promises);
    return res;
  }

  public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void) {
    this.connection.on('OnStateEvents', (domainName, eventText) => {
      eventHandler(domainName, JSON.parse(eventText));
    });
  }

  public onClose(eventHandler: (error: Error | undefined) => void) {
    if (this.connection.state == HubConnectionState.Disconnected) {
      eventHandler(undefined);
      return;
    }
    this.connection.onclose(eventHandler);
  }

  public stop(): Promise<void> {
    return this.connection.stop();
  }
}
