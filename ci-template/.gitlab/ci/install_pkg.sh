#!/bin/bash

if [[ $UNITY_VERSION == *2020* ]]; then
    echo "Installing 7z for Unity 2020 to shrink symbol."
	apt-get update
	apt-get -y install p7zip-full
else
    echo "Skip installing 7z."
fi