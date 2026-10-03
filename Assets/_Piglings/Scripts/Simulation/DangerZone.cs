using System.Collections.Generic;
using Piglings.Events;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// Trigger a little below the roof line (BarnTopZone). A climbing robot crossing it is about
    /// to breach: publishes RobotEnteredDangerZone, once per robot, all night long.
    ///
    /// Unlike BarnTopZone (a plain marker the robot checks for), this zone publishes the event
    /// itself: entering it changes nothing about the robot, so the robot shouldn't have to know
    /// about it. No robot state either — "in danger" is a warning for views, never a rule: it doesn't
    /// end or change the night. Not a phase of the robot's life.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DangerZone : MonoBehaviour
    {
        [SerializeField] private NightSession session;

        // Robots already announced. Ids are unique for the night, so this only grows by one per
        // robot that ever reaches the line — a few dozen a night at most.
        private readonly HashSet<GameId> _announced = new HashSet<GameId>();

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Only a climbing robot is "about to breach". A robot already breaking has lost its grip.
            if (!other.TryGetComponent(out RobotController robot) || !robot.CanLoseGrip) return;
            if (!_announced.Add(robot.Id)) return;   // once per robot, even if it re-enters
            session.Bus.Publish(new RobotEnteredDangerZone(robot.Id));
        }
    }
}
