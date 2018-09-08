export interface ObservableDomainEvent {
    N: number;
    E: [string, string, number | string][];
}
export interface ObservableDomainState {
    N: number;
    P: string[];
    O: any;
    R: string[];
}
export declare class ObservableDomain {
    private o;
    private props;
    private tranNum;
    graph: any;
    roots: any[];
    constructor(initialState: string | ObservableDomainState);
    applyEvent(event: ObservableDomainEvent): void;
    private getValue;
}
//# sourceMappingURL=index.d.ts.map