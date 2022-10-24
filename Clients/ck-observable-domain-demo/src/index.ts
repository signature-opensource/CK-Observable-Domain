import axios, { Axios } from "axios";
import { HttpCrisEndpoint } from "./services/http-cris-endpoint";
import { MqttObservableLeagueDomainService } from "@signature-code/ck-observable-domain-mqtt"
import { ObservableDomainClient } from "@signature-code/ck-observable-domain";
import { SignalRObservableWatcherStartOrRestartCommand, SliderCommand } from "@signature/generated";
import { SignalRObservableLeagueDomainService } from "@signature-code/ck-observable-domain-signalr";
const crisAxios = new Axios({
    baseURL: "http://localhost:5000/.cris"
});
const crisEndpoint = new HttpCrisEndpoint(crisAxios);

export async function startPage(): Promise<void> {

    const mqttOdDriver = new MqttObservableLeagueDomainService("ws://localhost:8080/mqtt/", crisEndpoint, "CK-OD");
    const signalROdDriver = new SignalRObservableLeagueDomainService("http://localhost:5000/hub/league", crisEndpoint);

    const mqttOdClient = new ObservableDomainClient(mqttOdDriver);
    // mqttOdClient.start();
    const signalrOdClient = new ObservableDomainClient(signalROdDriver);
    signalrOdClient.start();
    const mqttOdObs = await mqttOdClient.listenToDomain("Test-Domain");
    const signalROdObs = await signalrOdClient.listenToDomain("Test-Domain");
    const left = document.getElementById("slider-left") as HTMLInputElement;
    const right = document.getElementById("slider-right") as HTMLInputElement;

    mqttOdObs.forEach((s) =>  {
        if(s.length < 1) return;
        left.value = s[0].Slider
    });

    signalROdObs.forEach((s) =>  {
        if(s.length < 1) return;
        console.log(s[0].Slider);
        right.value = s[0].Slider;
    })
}

export async function sliderUpdate(sliderValue): Promise<void> {
    crisEndpoint.send<void>(SliderCommand.create(Number(sliderValue)));
}


startPage();

window["sliderUpdate"] = sliderUpdate;
