using System.Diagnostics;

internal static class Engine
{
    public static void Solve(int numberOfRoomsOrTimeSlots, int numberOfUsers, TimeSpan timeout)
    {
        var start = DateTime.Now;
        var numberOfRooms = numberOfRoomsOrTimeSlots;
        var numberOfTimeSlots = numberOfRoomsOrTimeSlots;

        var model = new CpModel();

        // Build up the model:

        var sameRoomConstraints = new List<BoolVar>();

        var users = Enumerable.Range(1, numberOfUsers)
            .Select(CreateUser)
            .ToArray();

        foreach (var user in users)
        {
            // User must be in different room for every time slot
            model.AddAllDifferent(user.TimeSlots.Select(timeSlot => timeSlot.Room));
        }

        foreach (var pair in users.GetAllPairs())
        {
            // Get all pairs of users and add boolean vars for every timeSlot that is true if both are in the same room for that time slot.
            var isInSameRoom = pair.Item1.TimeSlots
                .Zip(pair.Item2.TimeSlots)
                .Select(AddIsInSameRoomConstraint)
                .ToArray();

            // Two users should be in the same room exactly once
            model.AddExactlyOne(isInSameRoom);
        }

        // Solve the model:

        Console.WriteLine("Number of rooms: {0}", numberOfRooms);
        Console.WriteLine("Number of users: {0}", numberOfUsers);
        Console.WriteLine("Timeout: {0}", timeout.TotalMilliseconds < 0 ? "Infinite" : timeout);
        Console.WriteLine("Same room constraints: {0}", sameRoomConstraints.Count);
        Console.WriteLine();

        using var solver = new Solver(timeout);

        var status = solver.Solve(model);

        // Output results:

        Console.WriteLine("Solution: {0}", status);
        Console.WriteLine("Elapsed: {0:F3} min", (DateTime.Now - start).TotalMinutes);
        Console.WriteLine("Solutions: {0}", solver.NumberOfSolutions);
        Console.WriteLine();

        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            Console.WriteLine(solver.IsCancelledWithTimeout ? "Canceled after timeout." : "No solution found.");
            return;
        }

        Console.WriteLine("TimeSlot  | " + string.Join(" | ", Enumerable.Range(1, numberOfTimeSlots).Select(i => "T" + i.ToString("00"))));
        Console.WriteLine("----------|-" + string.Join("-|-", Enumerable.Range(1, numberOfTimeSlots).Select(_ => "---")));

        foreach (var user in users)
        {
            Console.WriteLine("{0,-10}| {1}", "User" + user.Id, string.Join(" | ", user.TimeSlots.Select(timeSlot => "R" + solver.Value(timeSlot.Room).ToString("00"))));
        }

        Console.WriteLine();

        var twoUsersInSameRoom = sameRoomConstraints
            .Where(item => solver.Value(item))
            .Select(item => item.Name())
            .ToArray();

        // Verify: Every user meets once with another user
        var isResultValid = twoUsersInSameRoom.Length == Enumerable.Range(0, numberOfUsers).Sum();
        
        Debug.Assert(isResultValid);

        Console.WriteLine("In same room: {0}, valid {1}", twoUsersInSameRoom.Length, isResultValid);

        foreach (var item in twoUsersInSameRoom.Take(20))
        {
            Console.WriteLine(item);
        }

        // Helper methods

        User CreateUser(int userId)
        {
            var timeSlots = Enumerable.Range(1, numberOfTimeSlots)
                .Select(timeSlotId => new TimeSlot(timeSlotId, model.NewIntVar(1, numberOfRooms, $"User {userId} TimeSlot {timeSlotId}")))
                .ToArray();

            return new User(userId, timeSlots);
        }

        BoolVar AddIsInSameRoomConstraint((TimeSlot First, TimeSlot Second) timeSlotPair)
        {
            var first = timeSlotPair.First.Room;
            var second = timeSlotPair.Second.Room;

            var isInSameRoom = model.NewBoolVar(first.Name() + " and " + second.Name());

            sameRoomConstraints.Add(isInSameRoom);

            // model.Add(first == second).OnlyEnforceIf(isInSameRoom);
            model.Add(first != second).OnlyEnforceIf(isInSameRoom.Not());

            return isInSameRoom;
        }
    }

    private record User(int Id, TimeSlot[] TimeSlots);

    private record TimeSlot(int Id, IntVar Room);

    private class Solver : SolutionCallback
    {
        private readonly TimeSpan _timeout;
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly CpSolver _solver = new CpSolver
        {
            // StringParameters = "enumerate_all_solutions:true"
        };


        public Solver(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public CpSolverStatus Solve(CpModel model)
        {
            Task.Delay(_timeout, _tokenSource.Token).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    IsCancelledWithTimeout = true;
                    StopSearch();
                }
            });

            return _solver.Solve(model, this);
        }

        public int NumberOfSolutions { get; private set; }

        public bool IsCancelledWithTimeout { get; private set; }

        public override void OnSolutionCallback()
        {
            NumberOfSolutions++;
            // Console.WriteLine("Booleans: {0}", NumBooleans());
        }

        protected override void Dispose(bool disposing)
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            
            base.Dispose(disposing);
        }

        public long Value(IntVar var)
        {
            return _solver.Value(var);
        }

        public bool Value(BoolVar var)
        {
            return _solver.BooleanValue(var);
        }
    }
}
