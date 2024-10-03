import axios from "axios";
import {MQTTObservableLeagueDomainService} from "@local/ck-gen"
import {ObservableDomainClient} from "@local/ck-gen";
import {HttpCrisEndpoint, SliderCommand} from "@local/ck-gen";
import {SignalRObservableLeagueDomainService} from "@local/ck-gen";

const crisAxios = axios.create();
const crisEndpoint = new HttpCrisEndpoint(crisAxios, "http://localhost:5000/.cris");

(async function (): Promise<void> {
    const mqttOdDriver = new MQTTObservableLeagueDomainService("ws://localhost:8080/mqtt/", crisEndpoint, "CK-OD");
    const signalROdDriver = new SignalRObservableLeagueDomainService("http://localhost:5000/hub/league", crisEndpoint);

    const mqttOdClient = new ObservableDomainClient(mqttOdDriver);
    mqttOdClient.start();
    const signalrOdClient = new ObservableDomainClient(signalROdDriver);
    signalrOdClient.start();
    const mqttOdObs = await mqttOdClient.listenToDomainAsync("Test-Domain");
    const signalROdObs = await signalrOdClient.listenToDomainAsync("Test-Domain");
    const left = document.getElementById("slider-left") as HTMLInputElement;
    const right = document.getElementById("slider-right") as HTMLInputElement;

    mqttOdObs.forEach((s: any) => {
        if (s.length < 1) return;
        console.log("Mqtt value: " + s[0].Slider);
        left.value = s[0].Slider
    });

    signalROdObs.forEach((s: any) => {
        if (s.length < 1) return;
        console.log("SignalR value: " + s[0].Slider);
        right.value = s[0].Slider;
    });
})();

export async function sliderUpdate(sliderValue: number): Promise<void> {
    await crisEndpoint.sendOrThrowAsync<void>( new SliderCommand(sliderValue) );
}


(<any>window)["sliderUpdate"] = sliderUpdate;
