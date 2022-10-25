import { WatchEvent } from '@signature-code/ck-observable-domain';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState, IRetryPolicy, RetryContext } from '@microsoft/signalr';
import { IObservableDomainLeagueDriver } from '@signature-code/ck-observable-domain/src/iod-league-driver';
import { ICrisEndpoint, SignalRObservableWatcherStartOrRestartCommand, VESACode } from "@signature/generated";

class NoRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: RetryContext): number | null {
    return null;
  }
}

export class SignalRObservableLeagueDomainService implements IObservableDomainLeagueDriver {
  private readonly connection: HubConnection;
  private static noRetryPolicity = new NoRetryPolicy();
  

  constructor(hubUrl: string, private readonly crisEndpoint: ICrisEndpoint) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, HttpTransportType.None)
      .withAutomaticReconnect(SignalRObservableLeagueDomainService.noRetryPolicity)
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
      const poco = SignalRObservableWatcherStartOrRestartCommand.create(this.connection.connectionId!, element.domainName, element.transactionCount);
      const resp = await this.crisEndpoint.send(poco);
      if (resp.code == 'CommunicationError') throw resp.result;
      if (resp.code != VESACode.Synchronous) throw new Error(JSON.stringify(resp.result));
      res[element.domainName] = JSON.parse(resp.result!);
    });
    await Promise.all(promises);
    return res;
  }

  public onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void) {
    this.connection.on('OnStateEventsAsync', (domainName: string, eventText: string) => {
      eventHandler(domainName, JSON.parse(eventText));
    });
  }

  public onClose(eventHandler: (error: Error | undefined) => void) {
    this.connection.onclose(eventHandler);
  }

  public async stop(): Promise<void> {
    await this.connection.stop();
  }
}
