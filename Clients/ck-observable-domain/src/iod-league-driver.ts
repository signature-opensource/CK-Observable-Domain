import { Observable } from 'rxjs';
import { DomainExportEvent, WatchEvent } from './observable-domain';


export interface IObservableDomainLeagueDriver {
    start(): Promise<boolean>;
    startListening(domainsNames: {domainName: string, transactionCount: number}[]): Promise<{[domainName: string]: WatchEvent}>;
    onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void;
    onClose(eventHandler: (error: Error | undefined) => void): void;
    stop(): Promise<void>;
}
