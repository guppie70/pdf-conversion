"""Background job manager for async PDF conversions."""

import asyncio
import logging
import uuid
from datetime import datetime, timedelta
from typing import Dict, Optional, Callable, Awaitable
from pathlib import Path

from models.job_models import JobStatus, JobInfo

logger = logging.getLogger(__name__)


class JobManager:
    """
    Manages background conversion jobs with progress tracking.

    Uses in-memory storage for simplicity. Jobs are processed sequentially
    since Docling is CPU-intensive.
    """

    def __init__(self, cleanup_age_hours: int = 1):
        """
        Initialize job manager.

        Args:
            cleanup_age_hours: Remove completed jobs older than this many hours
        """
        self.jobs: Dict[str, JobInfo] = {}
        self.cleanup_age = timedelta(hours=cleanup_age_hours)
        self._processing = False
        self._queue = asyncio.Queue()
        self._worker_task: Optional[asyncio.Task] = None
        logger.info("JobManager initialized")

    async def start_worker(self):
        """Start background worker task."""
        if self._worker_task is None:
            self._worker_task = asyncio.create_task(self._worker_loop())
            logger.info("Background worker started")

    async def stop_worker(self):
        """Stop background worker task."""
        if self._worker_task:
            self._worker_task.cancel()
            try:
                await self._worker_task
            except asyncio.CancelledError:
                pass
            self._worker_task = None
            logger.info("Background worker stopped")

    def create_job(
        self,
        project_id: str,
        filename: str,
        output_format: str = "docbook"
    ) -> str:
        """
        Create a new conversion job.

        Args:
            project_id: Project ID for organization
            filename: Original filename
            output_format: Target format

        Returns:
            job_id: Unique job identifier
        """
        job_id = str(uuid.uuid4())

        job = JobInfo(
            job_id=job_id,
            status=JobStatus.QUEUED,
            progress=0.0,
            message="Job queued for processing",
            project_id=project_id,
            filename=filename,
            output_format=output_format
        )

        self.jobs[job_id] = job
        logger.info(f"Created job {job_id}: project={project_id}, file={filename}")

        return job_id

    async def enqueue_job(
        self,
        job_id: str,
        conversion_func: Callable[[str, Callable[[float, int, int, str], Awaitable[None]]], Awaitable[tuple[Optional[str], Optional[int], Optional[str]]]]
    ):
        """
        Enqueue job for processing.

        Args:
            job_id: Job identifier
            conversion_func: Async function to execute conversion
                            Should accept (job_id, progress_callback) and return (output_file, page_count, error)
        """
        await self._queue.put((job_id, conversion_func))
        logger.info(f"Enqueued job {job_id} (queue size: {self._queue.qsize()})")

    def get_job(self, job_id: str) -> Optional[JobInfo]:
        """
        Get job status.

        Args:
            job_id: Job identifier

        Returns:
            JobInfo if found, None otherwise
        """
        return self.jobs.get(job_id)

    def update_progress(
        self,
        job_id: str,
        progress: float,
        current_page: Optional[int] = None,
        total_pages: Optional[int] = None,
        message: Optional[str] = None
    ):
        """
        Update job progress.

        Args:
            job_id: Job identifier
            progress: Progress value (0.0 to 1.0)
            current_page: Current page number
            total_pages: Total page count
            message: Status message
        """
        job = self.jobs.get(job_id)
        if job:
            job.progress = min(max(progress, 0.0), 1.0)
            if current_page is not None:
                job.current_page = current_page
            if total_pages is not None:
                job.total_pages = total_pages
            if message:
                job.message = message

            logger.debug(f"Job {job_id} progress: {job.progress:.1%} - {job.message}")

    def cancel_job(self, job_id: str) -> bool:
        """
        Cancel a job.

        Args:
            job_id: Job identifier

        Returns:
            True if cancelled, False if not found or already completed
        """
        job = self.jobs.get(job_id)
        if job and job.status in (JobStatus.QUEUED, JobStatus.PROCESSING):
            job.status = JobStatus.CANCELLED
            job.message = "Job cancelled by user"
            job.completed_at = datetime.utcnow()
            logger.info(f"Cancelled job {job_id}")
            return True
        return False

    def cleanup_old_jobs(self):
        """Remove completed jobs older than cleanup_age."""
        cutoff = datetime.utcnow() - self.cleanup_age
        to_remove = [
            job_id for job_id, job in self.jobs.items()
            if job.status in (JobStatus.COMPLETED, JobStatus.FAILED, JobStatus.CANCELLED)
            and job.completed_at
            and job.completed_at < cutoff
        ]

        for job_id in to_remove:
            del self.jobs[job_id]
            logger.info(f"Cleaned up old job {job_id}")

    async def _worker_loop(self):
        """Background worker that processes jobs from queue."""
        logger.info("Worker loop started")

        try:
            while True:
                # Cleanup old jobs periodically
                self.cleanup_old_jobs()

                # Get next job from queue (with timeout to allow periodic cleanup)
                try:
                    job_id, conversion_func = await asyncio.wait_for(
                        self._queue.get(),
                        timeout=60.0  # Check for cleanup every minute
                    )
                except asyncio.TimeoutError:
                    continue

                job = self.jobs.get(job_id)
                if not job:
                    logger.warning(f"Job {job_id} not found, skipping")
                    continue

                # Skip if already cancelled
                if job.status == JobStatus.CANCELLED:
                    logger.info(f"Job {job_id} was cancelled, skipping")
                    continue

                # Mark as processing
                job.status = JobStatus.PROCESSING
                job.started_at = datetime.utcnow()
                job.message = "Processing started"
                logger.info(f"Processing job {job_id}")

                try:
                    # Create progress callback
                    async def progress_callback(progress: float, current_page: int, total_pages: int, message: str):
                        self.update_progress(job_id, progress, current_page, total_pages, message)

                    # Execute conversion
                    output_file, page_count, error = await conversion_func(job_id, progress_callback)

                    # Update job with result
                    job.completed_at = datetime.utcnow()

                    if error:
                        job.status = JobStatus.FAILED
                        job.error = error
                        job.message = f"Conversion failed: {error}"
                        logger.error(f"Job {job_id} failed: {error}")
                    else:
                        job.status = JobStatus.COMPLETED
                        job.progress = 1.0
                        job.total_pages = page_count
                        job.message = f"Conversion completed ({page_count} pages)"
                        logger.info(f"Job {job_id} completed successfully")

                except Exception as e:
                    job.status = JobStatus.FAILED
                    job.error = str(e)
                    job.message = f"Conversion failed: {str(e)}"
                    job.completed_at = datetime.utcnow()
                    logger.error(f"Job {job_id} failed with exception: {e}", exc_info=True)

        except asyncio.CancelledError:
            logger.info("Worker loop cancelled")
            raise


# Global job manager instance
_job_manager: Optional[JobManager] = None


def get_job_manager() -> JobManager:
    """Get global job manager instance."""
    global _job_manager
    if _job_manager is None:
        _job_manager = JobManager()
    return _job_manager
