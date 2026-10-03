using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Egaku.Tests.Editor
{
    // Use a separate PhysicsScene2D: tests neither move production objects nor change global gravity.
    public sealed class RunnerGrabTests
    {
        private Scene scene;
        private PhysicsScene2D physics;
        private Rigidbody2D runner;
        private object movement;
        private Type movementType;
        private float time;
        private Rigidbody2D testSupport;

        [SetUp]
        public void SetUp()
        {
            // Preview scenes supply isolated 2D physics without entering Play Mode.
            scene = EditorSceneManager.NewPreviewScene();
            physics = scene.GetPhysicsScene2D();
            Assert.That(physics, Is.Not.EqualTo(Physics2D.defaultPhysicsScene));
            runner = Body("Runner", new Vector2(0, 10), Vector2.one, "Player");
            movementType = Type.GetType("RunnerMovement, Assembly-CSharp", true);
            object tuning = Activator.CreateInstance(Type.GetType("RunnerMovementTuning, Assembly-CSharp", true));
            object grab = Activator.CreateInstance(Type.GetType("RunnerGrabTuning, Assembly-CSharp", true));
            // Immediate parameter selection isolates the motor assertions from transition timing.
            grab.GetType().GetField("transitionTime").SetValue(grab, 0f);
            movement = Activator.CreateInstance(movementType, runner, runner.GetComponent<Collider2D>(), tuning, grab);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        private Rigidbody2D Body(string name, Vector2 position, Vector2 size, string layer)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = LayerMask.NameToLayer(layer);
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            return body;
        }

        private bool Step(float input = 0f, bool support = false, Rigidbody2D held = null)
        {
            // Tests that need a controlled support transition create real geometry,
            // rather than granting jump permission through a production boolean bypass.
            if (support && testSupport == null)
            {
                testSupport = Body("Test support", runner.position + Vector2.down, new Vector2(1, 1), "Platform");
                testSupport.bodyType = RigidbodyType2D.Static;
            }
            if (testSupport != null)
            {
                testSupport.gameObject.SetActive(support);
                if (support) testSupport.position = runner.position + Vector2.down;
                Physics2D.SyncTransforms();
            }
            time += Time.fixedDeltaTime;
            return (bool)movementType.GetMethod("FixedStep").Invoke(movement,
                new object[] { input, time, 10f, 22, held });
        }

        private void PressJump()
        {
            movementType.GetMethod("QueueJump").Invoke(movement, new object[] { time });
        }

        private bool Support(Rigidbody2D body)
        {
            return (bool)Type.GetType("GrabPhysics, Assembly-CSharp", true)
                .GetMethod("HasExternalSupport").Invoke(null, new object[] { body, runner });
        }

        [Test]
        public void AirborneHeldBoardCannotGrantFirstOrRepeatedJumps()
        {
            var board = Body("Board", new Vector2(0, 9), new Vector2(3, 0.5f), "Draw");
            board.tag = "Holding";
            for (int i = 0; i < 20; i++)
            {
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(Support(board), Is.False);
                PressJump();
                Assert.That(Step(0f, Support(board), board), Is.False);
            }
        }

        [Test]
        public void HeldSupportJumpIsConsumedUntilARealLanding()
        {
            var board = Body("Board", new Vector2(3, 10), Vector2.one, "Draw");
            // Feed the support transition separately from contact geometry to verify the jump latch.
            Step(0f, true, board);
            PressJump();
            Assert.That(Step(0f, true, board), Is.True);
            for (int i = 0; i < 3; i++)
            {
                PressJump();
                Assert.That(Step(0f, true, board), Is.False, "Lingering takeoff contact must not re-arm.");
            }
            for (int i = 0; i < 10; i++)
            {
                PressJump();
                Assert.That(Step(0f, false, board), Is.False);
            }
            runner.linearVelocity = Vector2.zero;
            PressJump();
            Assert.That(Step(0f, true, board), Is.True, "A genuine new landing must restore one jump.");
        }

        [Test]
        public void ReleasedBoardIsAValidLandingButRegrabbingCannotGrantAnotherJump()
        {
            var board = Body("Board", new Vector2(0, 9.2f), new Vector2(3, 0.5f), "Draw");
            board.tag = "Holding";
            Step(0f, true, board);
            PressJump();
            Assert.That(Step(0f, true, board), Is.True);
            Step(0f, false, board);
            runner.linearVelocity = Vector2.zero;
            // The released board enters the ordinary ground-probe range while still airborne.
            board.position = new Vector2(0, 9.24f);
            board.tag = "Wood";
            Physics2D.SyncTransforms();
            physics.Simulate(Time.fixedDeltaTime);
            PressJump();
            Assert.That(Step(), Is.True, "A released board below the feet is an ordinary landing.");
            board.tag = "Holding";
            PressJump();
            Assert.That(Step(0f, false, board), Is.False);
        }

        [Test]
        public void SupportRequiresExternalFloorAndDisappearsAfterLift()
        {
            var board = Body("Board", new Vector2(0, 1), Vector2.one, "Draw");
            var floor = Body("Floor", Vector2.zero, new Vector2(10, 1), "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            board.gravityScale = 1f;
            for (int i = 0; i < 10; i++) physics.Simulate(Time.fixedDeltaTime);
            Assert.That(Support(board), Is.True);
            board.position = new Vector2(0, 5);
            board.linearVelocity = Vector2.zero;
            physics.Simulate(Time.fixedDeltaTime);
            Assert.That(Support(board), Is.False);
        }

        [Test]
        public void HeldBoardAtTheSideCannotGrantJumpEvenWhenItTouchesFloor()
        {
            var board = Body("Side board", new Vector2(3, 10), Vector2.one, "Draw");
            board.tag = "Holding";
            var floor = Body("Board floor", new Vector2(3, 9), Vector2.one, "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            board.gravityScale = 1;
            for (int i = 0; i < 5; i++) physics.Simulate(Time.fixedDeltaTime);
            Assert.That(Support(board), Is.True);
            PressJump();
            Assert.That(Step(held: board), Is.False);
        }

        [Test]
        public void HeldBoardBelowFeetRequiresExternalSupport()
        {
            var board = Body("Foot board", new Vector2(0, 9.2f), new Vector2(3, 0.5f), "Draw");
            board.tag = "Holding";
            var floor = Body("External floor", new Vector2(0, 8.45f), new Vector2(10, 1), "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            board.gravityScale = 1;
            for (int i = 0; i < 5; i++) physics.Simulate(Time.fixedDeltaTime);
            Assert.That(Support(board), Is.True);
            PressJump();
            Assert.That(Step(held: board), Is.True);
            floor.gameObject.SetActive(false);
            for (int i = 0; i < 15; i++)
            {
                physics.Simulate(Time.fixedDeltaTime);
                PressJump();
                Assert.That(Step(held: board), Is.False);
            }
        }

        [TestCase(-2f)]
        [TestCase(2f)]
        public void ReleasingBoardUnderFeetRestoresJumpOnMovingSupport(float supportVelocity)
        {
            var board = Body("Moving foot board", new Vector2(0, 9.24f), new Vector2(3, 0.5f), "Draw");
            board.tag = "Holding";
            board.mass = 20;
            Step(support: true, held: board);
            PressJump();
            Assert.That(Step(support: true, held: board), Is.True);
            board.position = new Vector2(0, 9.24f);
            runner.linearVelocity = board.linearVelocity = new Vector2(0, supportVelocity);
            Step(held: board);
            Assert.That(movementType.GetProperty("Grounded").GetValue(movement), Is.False);
            // Release restores normal contact eligibility regardless of past ownership
            // or whether the platform is rising, falling, floating or touching land.
            board.tag = "Wood";
            PressJump();
            Assert.That(Step(), Is.True);
        }

        [Test]
        public void PassingUpwardNearAReleasedBoardDoesNotRenewJump()
        {
            var board = Body("Board", new Vector2(3, 10), Vector2.one, "Draw");
            Step(support: true);
            PressJump();
            Assert.That(Step(support: true), Is.True);
            Step();
            board.position = new Vector2(0, 8.99f);
            Physics2D.SyncTransforms();
            PressJump();
            Assert.That(Step(), Is.False, "Upward separation is not a fresh landing.");
        }

        [Test]
        public void ReleasedFloatingBoardRestoresConsumedJump()
        {
            var water = new GameObject("Water");
            SceneManager.MoveGameObjectToScene(water, scene);
            var trigger = water.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(30, 20);
            trigger.isTrigger = true;
            trigger.usedByEffector = true;
            var buoyancy = water.AddComponent<BuoyancyEffector2D>();
            buoyancy.density = 20;
            buoyancy.surfaceLevel = 10;
            buoyancy.linearDamping = 5;
            var board = Body("Floating wood", new Vector2(3, 10.2f), new Vector2(4, 0.5f), "Draw");
            board.mass = 20;
            board.gravityScale = 1;
            board.tag = "Wood";
            for (int i = 0; i < 60; i++) physics.Simulate(Time.fixedDeltaTime);
            Assert.That(Support(board), Is.False, "Buoyancy provides no solid floor contact.");
            Step(support: true);
            PressJump();
            Assert.That(Step(support: true), Is.True);
            runner.position = board.position + Vector2.up * 0.765f;
            runner.linearVelocity = board.linearVelocity;
            board.tag = "Holding";
            Step(held: board);
            board.tag = "Wood";
            PressJump();
            Assert.That(Step(), Is.True, "Released buoyant wood follows the same landing rule.");
        }

        [Test]
        public void EmptyHandAirControlIsUnchangedAndGrabUsesSeventyPercent()
        {
            Step(1f);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(35f * Time.fixedDeltaTime).Within(0.0001f));
            runner.linearVelocity = Vector2.zero;
            var board = Body("Board", new Vector2(3, 10), Vector2.one, "Draw");
            Step(1f, false, board);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(35f * 0.7f * Time.fixedDeltaTime).Within(0.0001f));
        }

        [Test]
        public void GrabSpeedIsEightInBothDirectionsAndBrakesWithoutThrowing()
        {
            var board = Body("Board", new Vector2(3, 10), Vector2.one, "Draw");
            for (int i = 0; i < 30; i++) Step(1f, true, board);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(8f).Within(0.001f));
            for (int i = 0; i < 30; i++) Step(-1f, true, board);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(-8f).Within(0.001f));
            for (int i = 0; i < 10; i++) Step(0f, true, board);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void WallBlocksOnlyTheDirectionIntoIt()
        {
            var board = Body("Board", new Vector2(3, 10), Vector2.one, "Draw");
            var wall = Body("Wall", new Vector2(4, 10), new Vector2(1, 8), "Platform");
            wall.bodyType = RigidbodyType2D.Static;
            board.linearVelocity = Vector2.right;
            physics.Simulate(Time.fixedDeltaTime);
            Step(1f, false, board);
            Assert.That(movementType.GetProperty("Blocked").GetValue(movement), Is.True);
            Assert.That(runner.linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
            Step(-1f, false, board);
            Assert.That(movementType.GetProperty("Blocked").GetValue(movement), Is.False);
            Assert.That(runner.linearVelocity.x, Is.LessThan(0f));
        }

        [TestCase(0.02f)]
        [TestCase(0.2f)]
        public void LowCeilingAllowsAnotherJumpWithoutRespawn(float clearance)
        {
            runner.position = new Vector2(0, 1);
            runner.gravityScale = 5f;
            var floor = Body("Floor", Vector2.zero, new Vector2(10, 1), "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            var ceiling = Body("Ceiling", new Vector2(0, 2 + clearance), new Vector2(10, 1), "Platform");
            ceiling.bodyType = RigidbodyType2D.Static;
            // Production floors often have just a Collider2D, without any Rigidbody2D.
            UnityEngine.Object.DestroyImmediate(floor);
            RecreateMotor();
            for (int i = 0; i < 10; i++) { Step(); physics.Simulate(Time.fixedDeltaTime); }
            PressJump();
            Assert.That(Step(), Is.True);
            for (int i = 0; i < 30; i++)
            {
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(Step(), Is.False, "An old held press must not auto-jump on recovery.");
            }
            PressJump();
            Assert.That(Step(), Is.True, "Blocked takeoff must recover while still supported.");
        }

        private void RecreateMotor()
        {
            movement = Activator.CreateInstance(movementType, runner, runner.GetComponent<Collider2D>(),
                Activator.CreateInstance(Type.GetType("RunnerMovementTuning, Assembly-CSharp", true)),
                Activator.CreateInstance(Type.GetType("RunnerGrabTuning, Assembly-CSharp", true)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DrawnMassCentreUsesLocalGeometryEvenWithoutSimulation(bool simulated)
        {
            var go = new GameObject("Drawn centre");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(40, 25, 0);
            go.transform.rotation = Quaternion.Euler(0, 0, 35);
            var body = go.AddComponent<Rigidbody2D>();
            body.simulated = simulated;
            var polygon = go.AddComponent<PolygonCollider2D>();
            polygon.points = new[] { new Vector2(2, 1), new Vector2(6, 1), new Vector2(6, 3), new Vector2(2, 3) };
            var type = Type.GetType("DrawMesh, Assembly-CSharp", true);
            var draw = go.AddComponent(type);
            type.GetField("rb2d").SetValue(draw, body);
            type.GetField("col2d").SetValue(draw, polygon);
            type.GetMethod("UpdateLocalMassCenter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(draw, null);
            Assert.That(body.centerOfMass.x, Is.EqualTo(4).Within(0.001f));
            Assert.That(body.centerOfMass.y, Is.EqualTo(2).Within(0.001f));
        }

        [TestCase(0f, 1.2f, false)]
        [TestCase(0f, -1.2f, false)]
        [TestCase(1.2f, 0f, false)]
        [TestCase(-1.2f, 0f, false)]
        [TestCase(0f, 1.2f, true)]
        [TestCase(0f, -1.2f, true)]
        public void RigidHeldPairMatchesEmptyHandJumpHeight(float x, float y, bool shortJump)
        {
            runner.gravityScale = 5f;
            RecreateMotor();
            float baseline = MeasureJump(null, shortJump);
            runner.position = new Vector2(0, 10);
            runner.linearVelocity = Vector2.zero;
            runner.gravityScale = 5f;
            var board = Body("Board", runner.position + new Vector2(x, y), Vector2.one, "Draw");
            runner.constraints = RigidbodyConstraints2D.None;
            board.constraints = RigidbodyConstraints2D.None;
            board.tag = "Holding";
            board.gravityScale = 1f;
            board.mass = 20f; // Grabbing now preserves wood density instead of reducing mass to one.
            var joint = runner.gameObject.AddComponent<FixedJoint2D>();
            joint.connectedBody = board;
            joint.frequency = 0f;
            joint.enableCollision = false;
            RecreateMotor();
            float heldHeight = MeasureJump(board, shortJump);
            Assert.That(heldHeight, Is.EqualTo(baseline).Within(0.08f));
        }

        private float MeasureJump(Rigidbody2D board, bool shortJump)
        {
            float start = runner.position.y;
            float peak = start;
            PressJump();
            Assert.That(Step(0, true, board), Is.True);
            // Remove the launch support before integration so a below-foot load
            // cannot become embedded in the artificial test floor.
            testSupport.gameObject.SetActive(false);
            for (int i = 0; i < 65; i++)
            {
                physics.Simulate(Time.fixedDeltaTime);
                peak = Mathf.Max(peak, runner.position.y);
                if (shortJump && i == 3) movementType.GetMethod("ReleaseJump").Invoke(movement, null);
                Step(0, false, board);
            }
            return peak - start;
        }

        [Test]
        public void UngrabbedHeavyWoodMovesButReverseInputDoesNotPullIt()
        {
            runner.position = new Vector2(0, 1);
            runner.gravityScale = 5f;
            var floor = Body("Floor", Vector2.zero, new Vector2(30, 1), "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            var board = Body("Board", new Vector2(1, 1), Vector2.one, "Draw");
            board.tag = "Wood";
            board.mass = 20f;
            board.gravityScale = 1f;
            RecreateMotor();
            for (int i = 0; i < 60; i++) { Step(1); physics.Simulate(Time.fixedDeltaTime); }
            Assert.That(board.position.x, Is.GreaterThan(2f), "A mass-20 board should be visibly pushable.");
            Assert.That(board.mass, Is.EqualTo(20f));
            board.linearVelocity = Vector2.zero;
            runner.linearVelocity = Vector2.zero;
            Step(-1);
            physics.Simulate(Time.fixedDeltaTime);
            Assert.That(board.linearVelocity.x, Is.GreaterThanOrEqualTo(-0.01f));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void RunnerCanPushFromInsideAnArch(float direction)
        {
            runner.position = new Vector2(direction * 0.8f, 1);
            runner.gravityScale = 5f;
            var floor = Body("Floor", Vector2.zero, new Vector2(40, 1), "Platform");
            floor.bodyType = RigidbodyType2D.Static;
            var arch = Body("Arch", new Vector2(0, 0.5f), Vector2.one, "Draw");
            UnityEngine.Object.DestroyImmediate(arch.GetComponent<BoxCollider2D>());
            var polygon = arch.gameObject.AddComponent<PolygonCollider2D>();
            // Concave arch with sloping inside surfaces, rather than a vertical box side.
            polygon.points = new[] { new Vector2(-2, 0), new Vector2(-1.5f, 1.5f), new Vector2(0, 2),
                new Vector2(1.5f, 1.5f), new Vector2(2, 0), new Vector2(1.65f, 0),
                new Vector2(1.2f, 1.2f), new Vector2(0, 1.65f), new Vector2(-1.2f, 1.2f), new Vector2(-1.65f, 0) };
            arch.mass = 20;
            arch.gravityScale = 1;
            arch.tag = "Wood";
            RecreateMotor();
            for (int i = 0; i < 100; i++) { Step(direction); physics.Simulate(Time.fixedDeltaTime); }
            Assert.That(arch.position.x * direction, Is.GreaterThan(1f));
        }

        [TestCase(1f, false)]
        [TestCase(4f, false)]
        [TestCase(8f, false)]
        [TestCase(1f, true)]
        [TestCase(4f, true)]
        [TestCase(8f, true)]
        public void WaterGrabWithOriginalMassDoesNotLaunchThePair(float width, bool submergedRunner)
        {
            var water = new GameObject("Water");
            SceneManager.MoveGameObjectToScene(water, scene);
            water.transform.position = Vector3.zero;
            var trigger = water.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(30, 20);
            trigger.isTrigger = true;
            trigger.usedByEffector = true;
            var buoyancy = water.AddComponent<BuoyancyEffector2D>();
            buoyancy.density = 20f;
            buoyancy.surfaceLevel = 10f;
            buoyancy.linearDamping = 5f;
            buoyancy.angularDamping = 5f;
            // Match Float.prefab: both the Runner and wood receive water forces.
            buoyancy.useColliderMask = true;
            buoyancy.colliderMask = -1;
            // Runner stays above lethal water while grabbing a submerged board, as in play.
            runner.position = new Vector2(0, 11.2f);
            var board = Body("Submerged wood", new Vector2(0, 9.5f), new Vector2(width, 1), "Draw");
            // Also exercise both bodies below the surface without relying on the game's
            // water-death callback to hide a solver impulse during that first contact.
            if (submergedRunner) water.transform.position = new Vector3(0, 8, 0);
            board.mass = 20;
            board.gravityScale = 1;
            board.tag = "Holding";
            runner.gravityScale = 5;
            var joint = runner.gameObject.AddComponent<FixedJoint2D>();
            joint.connectedBody = board;
            joint.frequency = 0;
            RecreateMotor();
            float maxSpeed = 0;
            // Runner.OnTriggerEnter2D revives and releases on water contact. Only that
            // first contact step is meaningful for a submerged Runner; the ordinary
            // above-water holder scenario must stay stable for the full interval.
            int steps = submergedRunner ? 1 : 30;
            for (int i = 0; i < steps; i++)
            {
                Step(0, false, board);
                physics.Simulate(Time.fixedDeltaTime);
                maxSpeed = Mathf.Max(maxSpeed, runner.linearVelocity.y);
            }
            Assert.That(board.mass, Is.EqualTo(20));
            Assert.That(maxSpeed, Is.LessThan(10f), "A submerged load must not launch its holder.");
        }
    }
}
