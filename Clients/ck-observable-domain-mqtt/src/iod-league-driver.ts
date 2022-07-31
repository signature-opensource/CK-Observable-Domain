import { Observable } from 'rxjs';
import { DomainExportEvent, WatchEvent } from '@signature-code/ck-observable-domain';


export interface IObservableDomainLeagueDriver {
    start(): Promise<boolean>;
    startListening(domainsNames: {domainName: string, transactionCount: number}[]): Promise<{[domainName: string]: WatchEvent}>;
    onMessage(eventHandler: (domainName: string, eventsJson: WatchEvent) => void): void;
    onClose(eventHandler: (error: Error) => void): void;
    stop(): Promise<void>;
}
