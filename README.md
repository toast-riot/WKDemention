## Description
This mod was made in an hour and is designed to be frustrating and inconvenient.
**It is not leaderboard legal**.

### You will forget.

## What Happens?
A timer of 1 minute is given. Once this timer ends, a flash will appear and your screen will freeze. The screen will then fade to a new frame showing a previous memory.
Pressing H will override this random set **once** during every reset, however if it is already set before you override it, **you will no longer be able to manually override it until the next reset**.
The memory is set randomly from the half way mark up to the end of the timer.

## Version 1.0.2 - Community Patch
* Changed the episode timer length from 6 minutes to only 1 minute.
* Added logic to detect when the player enters a new level/area and reset snapshot state.
* Updated player handling so it resets the timer/state when the player is missing or is dead.
* Added restart detection so the timer resets reliably when the game restarts, instead of checking the loading state directly.
