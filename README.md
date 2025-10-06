# RoboticsRover
Robotics Rover for University.


This was a final year coursework for a Rover designed to follow a path on the floor and track manually selected people.

**Stage 1 (Path Following) Reinforecment Learning Visual Neural Network:**

A Unity ML-Agents PPO-trained rover, using discrete actions (0 = left, 1 = right, 2 = forward) for direct sim-to-real transfer (WASD). Trained in infinite procedurally generated rope environments with randomized angles, it maximizes coverage, adherence, and alignment while minimizing drift and inefficiency. Evaluated by high score under 4K/8K steps.

*Architecture (PPO):* 
- Input(64x64 Grayscale image),
- 10M steps, batch=512,
- buffer=4096,
- lr=3e-4,
- beta=1e-3,
- ε=0.15,
- λ=0.95,
- Output(discreteVariable(0/1/2)) 

*Network:* 
- Nature CNN (Complex NN built for Visual games),
- 3×256 layers,
- LSTM (seq=16, mem=128),
- normalized

*Reward:*
- +1f for each new segment of rope the rover traverses over. (Cannot stack)
- -1f for falling off the rope.
- Optional Centering reward (Deprecated)

*Environment:*
- Random angled rope paths (infinite - episodes capped by max steps)
- Consistent local Y axis across all ropes for easy generation.
- Each Rope has a goal collider to spawn the next segment
- Each Rope has a seamStart transform to be a target for the perfect demo recording.

*Gail Demo:*
- Targeting child objects at the end of each rope segment (positioned precisely).
- Rotating (actions 0/1) until aligned within a predefined angular threshold.
- Moving forward (action 2) to reach the rope’s end.
- Constantly checking and prioritising Rotation to imitate recovery while on rope.
- On rope transition, target is switched to the child of the next rope segment, enabling continuous path tracking across the procedural track.
