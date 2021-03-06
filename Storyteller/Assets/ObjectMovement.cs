﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectMovement : MonoBehaviour {

    [SerializeField]
    private ParticleSystem inspirationLeapPS;

    [Header("Collision")]
    [SerializeField]
    private int horizontalRaycastNumber;
    [SerializeField]
    private int verticalRaycastNumber;

    private Collider2D objectCollider;
    private LayerMask collisionLayerMask;
    private Animator objectAnimator;

    private bool isGrounded = false;

    private const float BUFFER_LENGTH = 0.05f;
    private const string OBSTACLE_LAYER = "Obstacle";
    private const string BOUNDARY_TAG = "Boundary";
    private const int INVALID_INDEX = -1;

    [Header("Movement")]
    [SerializeField]
    [Range(0, 90)]
    private float maximumClimbableSlopeAngle = 60;
    [SerializeField]
    [Range(0, 90)]
    private float maximumDescendableSlopeAngle = 40;
    [SerializeField]
    private float maxHorizontalSpeed;
    [SerializeField]
    private float groundAcceleration;
    [SerializeField]
    private float airAcceleration;
    [SerializeField]
    private float jumpSpeed;
    [SerializeField]
    private float gravityScale;
    [SerializeField]
    private float maxFallGravityScale;
    private Vector2 velocity;
    [SerializeField]
    private float glidingSpeed;
    private float initialGravityScale;

    public Vector2 Velocity { get { return velocity; } set { velocity = value; } }

    // Multipliers
    private float gravityMultiplier = 1;
    public float GravityMultiplier { get { return gravityMultiplier; } set { gravityMultiplier = value; } }
    private bool isGliding = false;
    private float timeStartGliding = 0;

    private bool canMove = true;
    public bool CanMove { set { canMove = value; } }

    private Vector3 initialScale;

    private void Awake() {
        objectCollider = GetComponent<Collider2D>();
        collisionLayerMask = LayerMask.GetMask(OBSTACLE_LAYER);
        objectAnimator = GetComponent<Animator>();
        initialScale = transform.localScale;

        initialGravityScale = gravityScale;

        ParticleSystem.EmissionModule inspirationLeapEmission = inspirationLeapPS.emission;
        inspirationLeapEmission.enabled = false;
    }

    private void Update() {

        if (isGliding) {                                                       ,mm,                                                                    

            Vector2 targetGlidingVelocity = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow)) {
                targetGlidingVelocity.x -= glidingSpeed;
            }
            if (Input.GetKey(KeyCode.RightArrow)) {
                targetGlidingVelocity.x += glidingSpeed;
            }
            if (Input.GetKey(KeyCode.UpArrow)) {
                targetGlidingVelocity.y += glidingSpeed;
            }
            if (Input.GetKey(KeyCode.DownArrow)) {
                targetGlidingVelocity.y -= glidingSpeed;
            }
            
            velocity = Vector2.MoveTowards(velocity, targetGlidingVelocity, 16 * Time.deltaTime);

            return;
        }

        float targetHorizontalVelocity = 0;
        bool hasHorizontalInput = false;

        if (canMove) {
            if (Input.GetKey(KeyCode.LeftArrow)) {
                targetHorizontalVelocity -= maxHorizontalSpeed;
                hasHorizontalInput = true;
            }
            if (Input.GetKey(KeyCode.RightArrow)) {
                targetHorizontalVelocity += maxHorizontalSpeed;
                hasHorizontalInput = true;
            }
            if (Input.GetKey(KeyCode.UpArrow) && isGrounded) {
                velocity = new Vector2(velocity.x, jumpSpeed * Mathf.Pow(gravityMultiplier, .375f));
                isGrounded = false;
            }

            
            if (Input.GetKeyDown(KeyCode.LeftShift)) {
                ParticleSystem.EmissionModule focusLeapEmission = inspirationLeapPS.emission;
                focusLeapEmission.enabled = true;
                timeStartGliding = Time.time;
                //gravityMultiplier /= 2f;
            } else if (Input.GetKeyUp(KeyCode.LeftShift)) {
                if (inspirationLeapPS.isEmitting) {
                    ParticleSystem.EmissionModule focusLeapEmission = inspirationLeapPS.emission;
                    focusLeapEmission.enabled = false;
                }
                //gravityMultiplier *= 2f;
            }
        }

        if (isGrounded) {
            velocity.x = Mathf.MoveTowards(velocity.x, targetHorizontalVelocity, groundAcceleration * Time.deltaTime);
        } else {
            velocity.x = Mathf.MoveTowards(velocity.x, targetHorizontalVelocity, airAcceleration * Time.deltaTime);
        }

        if (!hasHorizontalInput) {
            if (isGrounded) {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, groundAcceleration * Time.deltaTime);
            } else {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, airAcceleration * Time.deltaTime);
            }
            objectAnimator.SetBool("isMoving", false);
        } else {
            objectAnimator.SetBool("isMoving", true);
        }

        if (velocity.x > 0) {
            transform.localScale = initialScale;
        } else if (velocity.x < 0) {
            transform.localScale = new Vector3(-initialScale.x, initialScale.y, initialScale.z);
        }

        velocity += Mathf.Pow(gravityMultiplier, 1.33f) * gravityScale * Physics2D.gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, maxFallGravityScale * Physics2D.gravity.y);

    }

    private void FixedUpdate() {
        if (velocity.x != 0) {
            ProcessHorizontalMovement();
        }
        if (isGrounded || velocity.y < 0) {
            ProcessVerticalDownwardsMovement();
        } else if (velocity.y > 0) {
            ProcessVerticalUpwardsMovement();
        }
    }

    private void ProcessHorizontalMovement() {

        Bounds objectColliderBounds = objectCollider.bounds;
        Vector2 startRaycastPoint = new Vector2(objectColliderBounds.center.x, objectColliderBounds.min.y + BUFFER_LENGTH);
        Vector2 endRaycastPoint = new Vector2(objectColliderBounds.center.x, objectColliderBounds.max.y - BUFFER_LENGTH);
        float raycastDistance = (objectColliderBounds.size.x / 2) + Mathf.Abs(velocity.x * Time.fixedDeltaTime);
        Vector2 raycastDirection = Mathf.Sign(velocity.x) > 0 ? Vector2.right : Vector2.left;
        float horizontalMovementDirection = Mathf.Sign(velocity.x);

        RaycastHit2D[] horizontalHits = new RaycastHit2D[horizontalRaycastNumber];
        float smallestRaycastFraction = Mathf.Infinity;
        int raycastIndexUsed = INVALID_INDEX;
        bool encounterWall = false;

        for (int i = 0; i < horizontalRaycastNumber; i++) {
            float lerpAmount = (float)i / (float)(horizontalRaycastNumber - 1);
            Vector2 raycastOrigin = Vector2.Lerp(startRaycastPoint, endRaycastPoint, lerpAmount);
            horizontalHits[i] = Physics2D.Raycast(raycastOrigin, raycastDirection, raycastDistance, collisionLayerMask);

            if (horizontalHits[i] && horizontalHits[i].fraction > 0) {
                if (Vector2.Angle(horizontalHits[i].normal, Vector2.up) > maximumClimbableSlopeAngle) {
                    encounterWall = true;
                    raycastIndexUsed = i;
                }
                if (horizontalHits[i].fraction < smallestRaycastFraction && horizontalHits[i].collider.tag == BOUNDARY_TAG) {
                    raycastIndexUsed = i;
                    smallestRaycastFraction = horizontalHits[i].fraction;
                }
            }

        }

        if (encounterWall && (horizontalHits[raycastIndexUsed].collider.tag == BOUNDARY_TAG || isGrounded)) {
            float distanceToObstacle = horizontalHits[raycastIndexUsed].fraction * raycastDistance - (objectColliderBounds.size.x / 2);
            transform.Translate(raycastDirection * distanceToObstacle);
            velocity = new Vector2(0, velocity.y);

        } else if (raycastIndexUsed != INVALID_INDEX && horizontalHits[raycastIndexUsed].collider.tag == BOUNDARY_TAG) {
            if (isGrounded) {
                Vector2 directionAlongGround = new Vector2(horizontalHits[raycastIndexUsed].normal.y * horizontalMovementDirection, -horizontalHits[raycastIndexUsed].normal.x * horizontalMovementDirection);
                transform.Translate(directionAlongGround * velocity.magnitude * Time.deltaTime);
            } else {
                float distanceToObstacle = horizontalHits[raycastIndexUsed].fraction * raycastDistance - (objectColliderBounds.size.x / 2);
                transform.Translate(raycastDirection * distanceToObstacle);
                isGrounded = true;

            }
        } else {
            RaycastHit2D[] backHorizontalHits = new RaycastHit2D[horizontalRaycastNumber];
            Vector2 backRaycastDirection = Mathf.Sign(velocity.x) > 0 ? Vector2.left : Vector2.right;

            for (int i = 0; i < horizontalRaycastNumber; i++) {
                float lerpAmount = (float)i / (float)(horizontalRaycastNumber - 1);
                Vector2 raycastOrigin = Vector2.Lerp(startRaycastPoint, endRaycastPoint, lerpAmount);
                backHorizontalHits[i] = Physics2D.Raycast(raycastOrigin, backRaycastDirection, raycastDistance, collisionLayerMask);

                if (backHorizontalHits[i] && backHorizontalHits[i].fraction > 0) {
                    if (backHorizontalHits[i].fraction < smallestRaycastFraction && Vector2.Angle(backHorizontalHits[i].normal, Vector2.up) <= maximumDescendableSlopeAngle) {
                        raycastIndexUsed = i;
                        smallestRaycastFraction = backHorizontalHits[i].fraction;
                    }
                }
            }

            if (raycastIndexUsed != INVALID_INDEX) {
                if (isGrounded) {
                    Vector2 directionAlongGround = new Vector2(backHorizontalHits[raycastIndexUsed].normal.y * horizontalMovementDirection, - backHorizontalHits[raycastIndexUsed].normal.x * horizontalMovementDirection);
                    transform.Translate(directionAlongGround * velocity.magnitude * Time.deltaTime);
                } else {
                    transform.Translate(new Vector3(velocity.x, 0, 0) * Time.deltaTime);
                }
            } else {
                transform.Translate(new Vector3(velocity.x, 0, 0) * Time.deltaTime);
            }
        }
    }

    private void ProcessVerticalDownwardsMovement() {

        Bounds objectColliderBounds = objectCollider.bounds;

        
        Vector2 startRaycastPoint = new Vector2(objectColliderBounds.min.x + BUFFER_LENGTH, objectColliderBounds.center.y);
        Vector2 endRaycastPoint = new Vector2(objectColliderBounds.max.x - BUFFER_LENGTH, objectColliderBounds.center.y);
        float raycastDistance = (objectColliderBounds.size.y / 2) + (isGrounded ? BUFFER_LENGTH : Mathf.Abs(velocity.y * Time.deltaTime));

        RaycastHit2D[] verticalHits = new RaycastHit2D[verticalRaycastNumber];
        float smallestRaycastFraction = Mathf.Infinity;
        int raycastIndexUsed = INVALID_INDEX;

        for (int i = 0; i < verticalRaycastNumber; i++) {
            float lerpAmount = i / (verticalRaycastNumber - 1);
            Vector2 raycastOrigin = Vector2.Lerp(startRaycastPoint, endRaycastPoint, lerpAmount);
            verticalHits[i] = Physics2D.Raycast(raycastOrigin, Vector2.down, raycastDistance, collisionLayerMask);

            if (verticalHits[i].fraction > 0 && verticalHits[i].fraction < smallestRaycastFraction) {
                raycastIndexUsed = i;
                smallestRaycastFraction = verticalHits[i].fraction;
            }
        }

        /*
        float boxcastDistance = (objectColliderBounds.size.y / 2) + (isGrounded ? BUFFER_LENGTH : Mathf.Abs(velocity.y * Time.deltaTime));
        RaycastHit2D verticalHit = Physics2D.BoxCast((Vector2)transform.position + new Vector2(0, BUFFER_LENGTH), new Vector2(objectColliderBounds.size.x - 2* BUFFER_LENGTH, BUFFER_LENGTH), 0, Vector2.down, boxcastDistance, collisionLayerMask);*/

        /* - new Vector2(0, objectColliderBounds.size.y / 4) // objectColliderBounds.size.y / 2*/
        if (raycastIndexUsed != INVALID_INDEX) {

            RaycastHit2D verticalHit = verticalHits[raycastIndexUsed];

            if (Vector2.Angle(verticalHit.normal, Vector2.up) > maximumClimbableSlopeAngle) {

                float slideDirectionX = (verticalHit.normal.x > 0) ? -1 : 1;
                Vector2 directionAlongGround = new Vector2(verticalHit.normal.y * slideDirectionX, -verticalHit.normal.x);
                float slideVelocityScale = Vector2.Angle(verticalHit.normal, Vector2.up) / 90f;
                Vector2 targetVelocity = directionAlongGround * velocity.y * slideVelocityScale;
                transform.Translate(directionAlongGround * velocity.y * slideVelocityScale * Time.fixedDeltaTime);
                velocity.x = Mathf.Max(velocity.x, targetVelocity.x);

            } else {
                isGrounded = true;
                float distanceToObstacle = verticalHits[raycastIndexUsed].fraction * raycastDistance - (objectColliderBounds.size.y / 2);
                transform.Translate(Vector3.down * distanceToObstacle);
                velocity = new Vector2(velocity.x, 0);
            }
            
        } else {
            transform.Translate(new Vector3(0, velocity.y, 0) * Time.deltaTime);
            isGrounded = false;
        }

        /*
        if (verticalHit.fraction > 0) {
            isGrounded = true;
            float distanceToObstacle = verticalHit.fraction * boxcastDistance - (objectColliderBounds.size.y / 2);
            transform.Translate(Vector3.down * distanceToObstacle);
            velocity = new Vector2(velocity.x, 0);
        } else {
            transform.Translate(new Vector3(0, velocity.y, 0) * Time.deltaTime);
            isGrounded = false;
        }*/

    }

    private void ProcessVerticalUpwardsMovement() {

        Bounds objectColliderBounds = objectCollider.bounds;


        Vector2 startRaycastPoint = new Vector2(objectColliderBounds.min.x + BUFFER_LENGTH, objectColliderBounds.center.y);
        Vector2 endRaycastPoint = new Vector2(objectColliderBounds.max.x - BUFFER_LENGTH, objectColliderBounds.center.y);
        float raycastDistance = (objectColliderBounds.size.y / 2) + (isGrounded ? BUFFER_LENGTH : Mathf.Abs(velocity.y * Time.deltaTime));

        RaycastHit2D[] verticalHits = new RaycastHit2D[verticalRaycastNumber];
        float smallestRaycastFraction = Mathf.Infinity;
        int raycastIndexUsed = INVALID_INDEX;

        for (int i = 0; i < verticalRaycastNumber; i++) {
            float lerpAmount = i / (verticalRaycastNumber - 1);
            Vector2 raycastOrigin = Vector2.Lerp(startRaycastPoint, endRaycastPoint, lerpAmount);
            verticalHits[i] = Physics2D.Raycast(raycastOrigin, Vector2.up, raycastDistance, collisionLayerMask);

            if (verticalHits[i].fraction > 0 && verticalHits[i].fraction < smallestRaycastFraction && verticalHits[i].collider.tag == BOUNDARY_TAG) {
                raycastIndexUsed = i;
                smallestRaycastFraction = verticalHits[i].fraction;
            }
        }

        /*
        float boxcastDistance = (objectColliderBounds.size.y / 2) + (isGrounded ? BUFFER_LENGTH : Mathf.Abs(velocity.y * Time.deltaTime));
        RaycastHit2D verticalHit = Physics2D.BoxCast((Vector2)transform.position + new Vector2(0, BUFFER_LENGTH), new Vector2(objectColliderBounds.size.x - 2* BUFFER_LENGTH, BUFFER_LENGTH), 0, Vector2.down, boxcastDistance, collisionLayerMask);*/

        /* - new Vector2(0, objectColliderBounds.size.y / 4) // objectColliderBounds.size.y / 2*/
        if (raycastIndexUsed != INVALID_INDEX) {

            RaycastHit2D verticalHit = verticalHits[raycastIndexUsed];

            float distanceToObstacle = verticalHits[raycastIndexUsed].fraction * raycastDistance - (objectColliderBounds.size.y / 2);
            transform.Translate(Vector3.up * distanceToObstacle);
            velocity = new Vector2(velocity.x, 0);
        } else {
            transform.Translate(new Vector3(0, velocity.y, 0) * Time.deltaTime);
            isGrounded = false;
        }

        /*
        if (verticalHit.fraction > 0) {
            isGrounded = true;
            float distanceToObstacle = verticalHit.fraction * boxcastDistance - (objectColliderBounds.size.y / 2);
            transform.Translate(Vector3.down * distanceToObstacle);
            velocity = new Vector2(velocity.x, 0);
        } else {
            transform.Translate(new Vector3(0, velocity.y, 0) * Time.deltaTime);
            isGrounded = false;
        }*/

    }

    public void StartGliding() {
        isGliding = true;
        isGrounded = false;
        gravityScale = 0;
        //velocity = velocity.normalized * glidingSpeed;
    }

    public void StopGliding() {
        isGliding = false;
        //velocity.y = Mathf.Min(velocity.y, maxHorizontalSpeed);
        gravityScale = initialGravityScale;
    }

}
