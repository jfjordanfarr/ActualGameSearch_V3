document.getElementById('search-form').addEventListener('submit', async (e) => {
  e.preventDefault();
  const q = document.getElementById('q').value.trim();
  if (!q) return;

  const [gamesResp, reviewsResp] = await Promise.all([
    fetch(`/api/search/games?q=${encodeURIComponent(q)}`),
    fetch(`/api/search/reviews?q=${encodeURIComponent(q)}`)
  ]);

  const games = document.getElementById('games');
  const reviews = document.getElementById('reviews');
  games.innerHTML = '';
  reviews.innerHTML = '';

  if (gamesResp.ok) {
    const data = await gamesResp.json();
    for (const g of data.items ?? []) {
      const li = document.createElement('li');
      li.className = 'list-group-item';
      li.textContent = `${g.gameTitle ?? g.title ?? g.gameId}`;
      games.appendChild(li);
    }
  }

  if (reviewsResp.ok) {
    const data = await reviewsResp.json();
    for (const r of data.items ?? []) {
      const li = document.createElement('li');
      li.className = 'list-group-item';
      li.textContent = `${r.excerpt ?? r.reviewId}`;
      reviews.appendChild(li);
    }
  }
});
