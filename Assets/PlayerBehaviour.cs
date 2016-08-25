using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerBehaviour : MonoBehaviour {

    public float motionForceFactor = 20f;
    public float angleDamping = 1f;
    public float dashImpulse = 20f;
    public float maxParticleRate = 10f;
    public float dashParticleRate = 100f;
    public float dashParticleDuration = 1f;

    public ParticleSystem dashParticles;

    public Animator catAnimator;

    [HideInInspector]
    public int index;

    private Rigidbody rigidBody;

    private bool emitDashParticles = false;

    private float catAngle = 0f;

    private float simulateTimeout = 0f;
    private float simulateA = 0f;
    private float simulateD = 0f;

    public enum Status
    {
        Unknown,
        Dead,
        Grounded
    }

    [HideInInspector]
    public Status status = Status.Unknown;

    public Color color
    {
        set
        {
            foreach (var meshRenderer in GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.material.color = value;
            }

            foreach (var meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                meshRenderer.material.color = value;
            }
        }
    }

	void Start () {
        rigidBody = GetComponent<Rigidbody>();
	}
	
    private IEnumerator EmitDashParticles()
    {
        emitDashParticles = true;
        yield return new WaitForSeconds(dashParticleDuration);
        emitDashParticles = false;
    }

    public void Restart(float y)
    {
        Vector3 random = Random.insideUnitSphere;
        transform.position = new Vector3(random.x * 20f, y +5f + random.y * 2f, random.z * 20f);
        rigidBody.velocity = Vector3.zero;
        status = Status.Unknown;
    }

    public void CheckStatus()
    {
        if (status != Status.Dead)
            status = Status.Unknown;
    }

    void OnCollisionStay(Collision collision)
    {
        if (emitDashParticles && collision.gameObject.tag == "Player")
        {
            rigidBody.AddExplosionForce(500f, collision.contacts[0].point, 10f);
            collision.gameObject.GetComponent<Rigidbody>().AddExplosionForce(500f, collision.contacts[0].point, 10f);
        }

        if (status == Status.Unknown && collision.gameObject.tag == "Ground")
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) > 0.8)
                    status = Status.Grounded;
            }
        }
    }

	void Update () {
        float x = Input.GetAxis(index + "X");
        float y = Input.GetAxis(index + "Y");
        float d = Mathf.Sqrt(x * x + y * y);
        float a = Mathf.Atan2(y, x);
        if (d < 0.1)
            d = 0;

        if (GameBehaviour.instance.simulatePlayers)
        {
            simulateTimeout -= Time.deltaTime;
            if (simulateTimeout < 0f)
            {
                simulateTimeout = 0.2f + Random.value * 0.5f;
                Vector3 position = transform.position;
                simulateA = Mathf.Atan2(position.z, position.x) + (Random.value - 0.5f);
                simulateD = Random.value;
            }

            a = simulateA;
            d = simulateD;
        }

        // Angle
        Vector3 forward = transform.forward;
        float currentAngle = Mathf.Atan2(-forward.z, -forward.x);
        float angleDiff = currentAngle - a;
        while (angleDiff < -Mathf.PI)
            angleDiff += 2 * Mathf.PI;
        while (angleDiff >= Mathf.PI)
            angleDiff -= 2 * Mathf.PI;
        angleDiff *= 1f - Mathf.Exp(-angleDamping * Time.deltaTime);
        transform.Rotate(0f, d * angleDiff * 180f / Mathf.PI, 0f);

        catAngle += (d * angleDiff * 10f - catAngle) * (1f - Mathf.Exp(-5f * Time.deltaTime));
        catAnimator.SetFloat("Angle", catAngle);

        rigidBody.AddRelativeForce(0f, 0f, d * motionForceFactor);

        dashParticles.emissionRate = emitDashParticles ? dashParticleRate : d * maxParticleRate;
        
        if (Input.GetButtonDown(index + "A") || GameBehaviour.instance.simulatePlayers && Random.value < 0.02f)
        {
            float targetAngle;
            forward = transform.forward;
            currentAngle = Mathf.Atan2(forward.z, forward.x);
            if (GameBehaviour.instance.HelpDirection(index, currentAngle, out targetAngle))
            {
                transform.eulerAngles = new Vector3(0f, 90f - targetAngle * 180f / Mathf.PI, 0f);
            }
            rigidBody.AddRelativeForce(0f, 0f, dashImpulse, ForceMode.Impulse);
            StartCoroutine("EmitDashParticles");
            catAnimator.SetTrigger("Dash");
        }

        if (status != Status.Dead && transform.position.y < -5f)
        {
            status = Status.Dead;
            GameBehaviour.instance.CheckStatuses();
        }
	}
}
